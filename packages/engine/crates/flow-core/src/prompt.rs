//! Deterministic, non-executable prompt templates shared by preview and export.
use crate::project::{Project, Result, Step};
use std::path::Path;

pub const DEFAULT_TEMPLATE: &str = r"# {{title}}

{{#if task}}
## Task

{{task}}
{{/if}}
{{#if context}}
## User context (not observed evidence)

{{context}}
{{/if}}
Duration: {{duration}}

[Play recording]({{recording_path}})

{{#moments}}
## Step {{number}} — {{title}}

Time: {{time}}

![Step {{number}}]({{screenshot_path}})

{{#if action}}
**Observed action:** {{action}}
{{/if}}
{{#if result}}
**Visible result:** {{result}}
{{/if}}
{{#if uncertainty}}
**Uncertainty:** {{uncertainty}}
{{/if}}
{{#if review_note}}
{{review_note}}
{{/if}}
{{/moments}}";

pub fn literal(text: &str) -> String {
    let mut output = String::new();
    for c in text.chars() {
        match c {
            '&' => output.push_str("&amp;"),
            '<' => output.push_str("&lt;"),
            '>' => output.push_str("&gt;"),
            '\\' | '`' | '*' | '_' | '{' | '}' | '[' | ']' | '(' | ')' | '#' | '!' | '|' => {
                output.push('\\');
                output.push(c);
            }
            _ => output.push(c),
        }
    }
    output
}

fn timestamp(ms: u64) -> String {
    format!("{:02}:{:02}.{:03}", ms / 60000, ms / 1000 % 60, ms % 1000)
}

fn link(path: &str) -> String {
    let path = if let Some(unc) = path.strip_prefix(r"\\?\UNC\") {
        format!("//{unc}")
    } else {
        path.strip_prefix(r"\\?\").unwrap_or(path).to_owned()
    };
    path.replace('\\', "/")
        .replace('%', "%25")
        .replace(' ', "%20")
        .replace('(', "%28")
        .replace(')', "%29")
        .replace('#', "%23")
        .replace('\n', "%0A")
        .replace('\r', "%0D")
        .replace('<', "%3C")
        .replace('>', "%3E")
}

pub fn review_note(step: &Step) -> String {
    let mut notes = Vec::new();
    if step.action.is_empty() && step.result.is_empty() {
        notes.push("Description not generated.");
    }
    if step.status == "stale" {
        notes.push("Review needed: screenshot context changed after this description was written.");
    } else if !step.reviewed && (!step.action.is_empty() || !step.result.is_empty()) {
        notes.push("Review status: not yet reviewed by the user.");
    }
    notes.join("\n\n")
}

enum Node<'a> {
    Text(&'a str),
    Token(&'a str, &'a str),
    Block(&'a str, &'a str, Vec<Node<'a>>),
}

// A malformed block is retained as one literal span, never partially interpolated.
fn parse<'a>(source: &'a str, depth: usize) -> Vec<Node<'a>> {
    if depth >= 64 {
        return vec![Node::Text(source)];
    }
    let mut nodes = Vec::new();
    let mut offset = 0;
    while let Some(start) = source[offset..].find("{{").map(|n| n + offset) {
        nodes.push(Node::Text(&source[offset..start]));
        let Some(end) = source[start + 2..].find("}}").map(|n| n + start + 4) else {
            nodes.push(Node::Text(&source[start..]));
            return nodes;
        };
        let token = source[start + 2..end - 2].trim();
        if let Some(open) = token.strip_prefix('#') {
            let kind = open.split_whitespace().next().unwrap_or("");
            let mut stack = vec![kind];
            let mut scan = end;
            let mut matched = None;
            while let Some(a) = source[scan..].find("{{").map(|n| n + scan) {
                let Some(b) = source[a + 2..].find("}}").map(|n| n + a + 4) else {
                    break;
                };
                let marker = source[a + 2..b - 2].trim();
                if let Some(inner) = marker.strip_prefix('#') {
                    stack.push(inner.split_whitespace().next().unwrap_or(""));
                } else if let Some(close) = marker.strip_prefix('/') {
                    if stack.last().copied() != Some(close) {
                        break;
                    }
                    stack.pop();
                    if stack.is_empty() {
                        matched = Some((a, b));
                        break;
                    }
                }
                scan = b;
            }
            if let Some((close, finish)) = matched {
                let raw = &source[start..finish];
                nodes.push(Node::Block(
                    open,
                    raw,
                    parse(&source[end..close], depth + 1),
                ));
                offset = finish;
            } else {
                nodes.push(Node::Text(&source[start..]));
                return nodes;
            }
        } else {
            nodes.push(Node::Token(token, &source[start..end]));
            offset = end;
        }
    }
    nodes.push(Node::Text(&source[offset..]));
    nodes
}

struct Context<'a> {
    project: &'a Project,
    directory: &'a Path,
    export: bool,
    moment: Option<(usize, &'a Step)>,
}
impl Context<'_> {
    fn value(&self, name: &str) -> Option<String> {
        let project = self.project;
        if let Some((number, step)) = self.moment {
            match name {
                "number" => return Some(number.to_string()),
                "title" => return Some(step.title.clone()),
                "time" => return Some(timestamp(project.frames.get(step.frame)?.time_ms)),
                "action" => return Some(step.action.clone()),
                "result" => return Some(step.result.clone()),
                "uncertainty" => return Some(step.uncertainty.clone()),
                "review_note" => return Some(review_note(step)),
                "screenshot_path" => {
                    return Some(if self.export {
                        format!("screenshots/{number:03}.png")
                    } else {
                        link(
                            &self
                                .directory
                                .join(&project.frames.get(step.frame)?.file)
                                .to_string_lossy(),
                        )
                    });
                }
                _ => {}
            }
        }
        match name {
            "title" => Some(project.title.clone()),
            "task" => Some(project.task.clone()),
            "context" => Some(project.context.clone()),
            "duration" => Some(timestamp(project.duration_ms)),
            "recording_path" => Some(if self.export {
                "recording.gif".into()
            } else {
                link(&self.directory.to_string_lossy())
            }),
            _ => None,
        }
    }
}

fn nested_moments(nodes: &[Node<'_>]) -> bool {
    nodes.iter().any(|n| match n {
        Node::Block(name, _, children) => *name == "moments" || nested_moments(children),
        _ => false,
    })
}
fn evaluate(nodes: &[Node<'_>], context: &Context<'_>, output: &mut String) -> Result<()> {
    for node in nodes {
        match node {
            Node::Text(text) => output.push_str(text),
            Node::Token(name, raw) => match context.value(name) {
                Some(value) => {
                    output.push_str(&if matches!(*name, "recording_path" | "screenshot_path") {
                        value
                    } else {
                        literal(&value)
                    })
                }
                None => output.push_str(raw),
            },
            Node::Block(name, raw, children) if *name == "moments" => {
                if context.moment.is_some() || nested_moments(children) {
                    output.push_str(raw);
                    continue;
                }
                let mut steps: Vec<_> = context.project.steps.iter().collect();
                steps.sort_by_key(|s| s.frame);
                for (index, step) in steps.into_iter().enumerate() {
                    evaluate(
                        children,
                        &Context {
                            moment: Some((index + 1, step)),
                            ..*context
                        },
                        output,
                    )?;
                }
            }
            Node::Block(name, raw, children) => {
                if let Some(field) = name.strip_prefix("if ").map(str::trim) {
                    match context.value(field) {
                        Some(value) if !value.is_empty() => evaluate(children, context, output)?,
                        Some(_) => {}
                        None => output.push_str(raw),
                    }
                } else {
                    output.push_str(raw);
                }
            }
        }
        if output.len() > 6 * 1024 * 1024 {
            return Err(
                "Rendered prompt exceeds 6 MiB; reduce the template or selected moments".into(),
            );
        }
    }
    Ok(())
}

pub fn render(project: &Project, directory: &Path, template: &str, export: bool) -> Result<String> {
    // Bounded input prevents accidental oversized preferences and protocol responses.
    if template.len() > 262_144 {
        return Err("Prompt template must be at most 256 KiB".into());
    }
    let mut output = String::new();
    evaluate(
        &parse(template, 0),
        &Context {
            project,
            directory,
            export,
            moment: None,
        },
        &mut output,
    )?;
    if !project.error.is_empty() {
        output.push_str(&format!(
            "\n\nRecording note: {}\n",
            literal(&project.error)
        ));
    }
    if output.len() > 6 * 1024 * 1024 {
        return Err(
            "Rendered prompt exceeds 6 MiB; reduce the template or selected moments".into(),
        );
    }
    Ok(output)
}
