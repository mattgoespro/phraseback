use flow_core::{
    project::Project,
    prompt::{DEFAULT_TEMPLATE, render},
};
use std::path::Path;

fn fixture() -> Project {
    serde_json::from_str(r#"{"title":"Document","task":"Fix this","context":"User context","frames":[{"file":"frames/a.png","time_ms":125},{"file":"frames/b.png","time_ms":250}],"steps":[{"frame":1,"title":"Second","action":"Click","result":"Opened"},{"frame":0,"title":"First"}]}"#).unwrap()
}

#[test]
fn scopes_order_conditions_and_unknown_syntax() {
    let project = fixture();
    assert_eq!(
        render(
            &project,
            Path::new("recording"),
            "{{title}} {{task}} {{#moments}}{{number}}:{{title}};{{/moments}}",
            true
        )
        .unwrap(),
        "Document Fix this 1:First;2:Second;"
    );
    assert_eq!(
        render(
            &project,
            Path::new("recording"),
            "{{#if task}}A{{#if context}}B{{/if}}{{/if}} {{unknown}} {{#if missing}}C{{/if}}",
            true
        )
        .unwrap(),
        "AB {{unknown}} {{#if missing}}C{{/if}}"
    );
    for text in [
        "{{#moments}}incomplete",
        "{{#if task}}a{{/moments}}",
        "{{#moments}}{{#moments}}{{title}}{{/moments}}{{/moments}}",
        "{{title",
    ] {
        assert_eq!(
            render(&project, Path::new("recording"), text, true).unwrap(),
            text
        );
    }
}

#[test]
fn default_omits_empty_task_preserves_notices_and_export_paths() {
    let mut project = fixture();
    project.task.clear();
    let text = render(&project, Path::new("recording"), DEFAULT_TEMPLATE, true).unwrap();
    assert!(!text.contains("## Task"));
    assert!(text.contains("Description not generated."));
    assert!(text.contains("not yet reviewed"));
    assert!(text.contains("screenshots/001.png"));
    project.steps[0].status = "stale".into();
    assert!(
        render(&project, Path::new("recording"), DEFAULT_TEMPLATE, false)
            .unwrap()
            .contains("screenshot context changed")
    );
}

#[test]
fn interpolated_text_is_literal_and_never_reparsed() {
    let mut project = fixture();
    project.task = "日本語 {{title}} <script>".into();
    let result = render(&project, Path::new("recording"), "{{task}}", false).unwrap();
    assert!(result.contains("日本語"));
    assert!(!result.contains("Document"));
    assert!(result.contains("&lt;script&gt;"));
}

#[test]
fn recording_notes_are_identical_in_preview_and_export_and_output_is_bounded() {
    let mut project = fixture();
    project.error = "Capture ended early".into();
    let preview = render(&project, Path::new("recording"), "{{title}}", false).unwrap();
    assert_eq!(
        preview,
        render(&project, Path::new("recording"), "{{title}}", true).unwrap()
    );
    assert!(preview.contains("Capture ended early"));
    assert!(
        render(
            &project,
            Path::new("recording"),
            &"x".repeat(262_145),
            false
        )
        .is_err()
    );
    project.steps = vec![project.steps[0].clone(); 1000];
    project.steps[0].action = "x".repeat(1024 * 1024);
    assert!(render(&project, Path::new("recording"), "{{#moments}}{{action}} {{action}} {{action}} {{action}} {{action}} {{action}} {{action}}{{/moments}}", false).is_err());
}

#[test]
fn native_extended_paths_are_readable_markdown_links() {
    let project = fixture();
    assert_eq!(
        render(
            &project,
            Path::new(r"\\?\C:\My recording"),
            "{{recording_path}}",
            false
        )
        .unwrap(),
        "C:/My%20recording"
    );
}
