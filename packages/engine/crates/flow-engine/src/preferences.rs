use flow_core::{
    project::{Result, atomic_json},
    protocol::commands::UiPreferences,
};
use std::{fs, path::Path};

const FILE: &str = "ui-preferences-rust-v1.json";
const TEMPLATE_FILE: &str = "prompt-template-rust-v1.json";

pub fn load_template(root: &Path) -> Result<flow_core::protocol::commands::PromptTemplate> {
    let default_template = flow_core::prompt::DEFAULT_TEMPLATE.to_owned();
    let template = match fs::read(root.join(TEMPLATE_FILE)) {
        Ok(bytes) => serde_json::from_slice::<String>(&bytes)
            .ok()
            .filter(|value| value.len() <= 262_144)
            .unwrap_or_else(|| default_template.clone()),
        Err(error) if error.kind() == std::io::ErrorKind::NotFound => default_template.clone(),
        Err(error) => return Err(error.into()),
    };
    Ok(flow_core::protocol::commands::PromptTemplate {
        template,
        default_template,
    })
}

pub fn save_template(
    root: &Path,
    template: String,
) -> Result<flow_core::protocol::commands::PromptTemplate> {
    if template.len() > 262_144 {
        return Err("Prompt template must be at most 256 KiB".into());
    }
    atomic_json(&root.join(TEMPLATE_FILE), &template)?;
    load_template(root)
}

fn defaults() -> UiPreferences {
    UiPreferences {
        width: 1280,
        height: 800,
        maximized: false,
        theme: Some("dark".into()),
    }
}

fn validate(value: &UiPreferences) -> Result<()> {
    if !(1024..=8192).contains(&value.width) || !(700..=8192).contains(&value.height) {
        return Err(
            "Window dimensions must be between 1024 x 700 and 8192 x 8192 logical pixels".into(),
        );
    }
    if !matches!(value.theme.as_deref(), None | Some("light" | "dark")) {
        return Err("Theme must be light or dark".into());
    }
    Ok(())
}

pub fn load(root: &Path) -> Result<UiPreferences> {
    let path = root.join(FILE);
    let bytes = match fs::read(&path) {
        Ok(bytes) => bytes,
        Err(error) if error.kind() == std::io::ErrorKind::NotFound => return Ok(defaults()),
        Err(error) => return Err(error.into()),
    };
    // A damaged preference file must not prevent evidence recovery. It is left
    // untouched until the user successfully saves new window preferences.
    Ok(serde_json::from_slice::<UiPreferences>(&bytes)
        .ok()
        .filter(|value| validate(value).is_ok())
        .unwrap_or_else(defaults))
}

pub fn save(root: &Path, value: UiPreferences) -> Result<UiPreferences> {
    validate(&value)?;
    atomic_json(&root.join(FILE), &value)?;
    Ok(value)
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn template_preferences_preserve_old_preferences_and_rejected_saves() {
        let root = tempfile::tempdir().unwrap();
        save(root.path(), defaults()).unwrap();
        let before = fs::read(root.path().join(FILE)).unwrap();
        assert_eq!(
            load_template(root.path()).unwrap().template,
            flow_core::prompt::DEFAULT_TEMPLATE
        );
        save_template(root.path(), "{{task}}".into()).unwrap();
        assert!(save_template(root.path(), "x".repeat(262_145)).is_err());
        assert_eq!(load_template(root.path()).unwrap().template, "{{task}}");
        assert_eq!(fs::read(root.path().join(FILE)).unwrap(), before);
        fs::write(root.path().join(TEMPLATE_FILE), b"{").unwrap();
        assert_eq!(
            load_template(root.path()).unwrap().template,
            flow_core::prompt::DEFAULT_TEMPLATE
        );
        assert_eq!(fs::read(root.path().join(TEMPLATE_FILE)).unwrap(), b"{");
    }

    #[test]
    fn preferences_round_trip_separately_and_invalid_save_preserves_bytes() {
        let root = tempfile::tempdir().unwrap();
        assert_eq!(load(root.path()).unwrap().width, 1280);
        fs::write(root.path().join("project.json"), b"legacy metadata").unwrap();
        save(
            root.path(),
            UiPreferences {
                width: 1400,
                height: 900,
                maximized: true,
                theme: Some("dark".into()),
            },
        )
        .unwrap();
        let before = fs::read(root.path().join(FILE)).unwrap();
        assert!(load(root.path()).unwrap().maximized);
        assert!(
            save(
                root.path(),
                UiPreferences {
                    width: 0,
                    height: 900,
                    maximized: false,
                    theme: None,
                }
            )
            .is_err()
        );
        assert_eq!(fs::read(root.path().join(FILE)).unwrap(), before);
        assert_eq!(
            fs::read(root.path().join("project.json")).unwrap(),
            b"legacy metadata"
        );
        assert_eq!(load(root.path()).unwrap().theme.as_deref(), Some("dark"));
    }

    #[test]
    fn old_preferences_load_and_invalid_theme_does_not_replace_them() {
        let root = tempfile::tempdir().unwrap();
        fs::write(
            root.path().join(FILE),
            br#"{"width":1280,"height":800,"maximized":false}"#,
        )
        .unwrap();
        assert_eq!(load(root.path()).unwrap().theme, None);
        let original = fs::read(root.path().join(FILE)).unwrap();
        assert!(
            save(
                root.path(),
                UiPreferences {
                    width: 1280,
                    height: 800,
                    maximized: false,
                    theme: Some("unknown".into())
                }
            )
            .is_err()
        );
        assert_eq!(fs::read(root.path().join(FILE)).unwrap(), original);
    }

    #[test]
    fn corrupt_preferences_fall_back_without_rewriting_and_failed_save_is_visible() {
        let root = tempfile::tempdir().unwrap();
        fs::write(root.path().join(FILE), b"{").unwrap();
        assert_eq!(load(root.path()).unwrap().height, 800);
        assert_eq!(fs::read(root.path().join(FILE)).unwrap(), b"{");
        fs::remove_file(root.path().join(FILE)).unwrap();
        fs::create_dir(root.path().join(FILE)).unwrap();
        assert!(save(root.path(), defaults()).is_err());
    }
}
