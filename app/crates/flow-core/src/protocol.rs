use serde::{Deserialize, Serialize};
use serde_json::Value;
use std::io::{self, Read, Write};

pub const MAJOR: u32 = 1;
pub const MAX_MESSAGE: usize = 8 * 1024 * 1024;

include!("protocol_generated.rs");

pub mod commands {
    include!("commands_generated.rs");
}

pub fn read_message<R: Read>(stream: &mut R) -> io::Result<Option<Request>> {
    let mut header = [0_u8; 4];
    if stream.read(&mut header[..1])? == 0 {
        return Ok(None);
    }
    stream.read_exact(&mut header[1..])?;
    let size = u32::from_le_bytes(header) as usize;
    if size == 0 || size > MAX_MESSAGE {
        return Err(io::Error::new(
            io::ErrorKind::InvalidData,
            "Invalid protocol payload length",
        ));
    }
    let mut payload = vec![0; size];
    stream.read_exact(&mut payload)?;
    let request: Request = serde_json::from_slice(&payload)
        .map_err(|_| io::Error::new(io::ErrorKind::InvalidData, "Invalid protocol request"))?;
    if request.id > i64::MAX as u64 || !request.params.is_object() {
        return Err(io::Error::new(
            io::ErrorKind::InvalidData,
            "Invalid request identity or parameters",
        ));
    }
    Ok(Some(request))
}

pub fn write_message<W: Write>(stream: &mut W, response: &impl Serialize) -> io::Result<()> {
    let bytes = serde_json::to_vec(response)?;
    if bytes.len() > MAX_MESSAGE {
        return Err(io::Error::new(
            io::ErrorKind::InvalidData,
            "Response exceeds limit",
        ));
    }
    stream.write_all(&(bytes.len() as u32).to_le_bytes())?;
    stream.write_all(&bytes)?;
    stream.flush()
}

#[cfg(test)]
mod tests {
    use super::*;
    #[test]
    fn rejects_truncation_and_oversized_messages() {
        assert!(read_message(&mut &b"\x10\x00"[..]).is_err());
        assert!(read_message(&mut &(MAX_MESSAGE as u32 + 1).to_le_bytes()[..]).is_err());
        assert!(read_message(&mut &[][..]).unwrap().is_none());
    }
    #[test]
    fn golden_handshake_parses() {
        let golden = include_bytes!("../../../contracts/hello.request.json");
        let request: Request = serde_json::from_slice(golden).unwrap();
        assert_eq!(request.method, "hello");
        assert_eq!(request.protocol, MAJOR);
    }
    #[test]
    fn golden_responses_parse_and_round_trip() {
        for golden in [
            include_bytes!("../../../contracts/hello.response.json").as_slice(),
            include_bytes!("../../../contracts/error.response.json").as_slice(),
        ] {
            let response: Response = serde_json::from_slice(golden).unwrap();
            assert_eq!(response.protocol, MAJOR);
            assert_ne!(response.result.is_some(), response.error.is_some());
            assert_eq!(
                serde_json::to_value(&response).unwrap(),
                serde_json::from_slice::<Value>(golden).unwrap()
            );
        }
    }
    #[test]
    fn golden_notification_parses_and_round_trips() {
        let golden = include_bytes!("../../../contracts/operation.notification.json");
        let notification: Notification = serde_json::from_slice(golden).unwrap();
        assert_eq!(notification.event, "operation_status");
        assert_eq!(notification.data["finished"], true);
        assert_eq!(
            serde_json::to_value(notification).unwrap(),
            serde_json::from_slice::<Value>(golden).unwrap()
        );
    }

    #[test]
    fn shared_command_goldens_validate_in_both_directions() {
        let golden: Value =
            serde_json::from_slice(include_bytes!("../../../contracts/commands.golden.json"))
                .unwrap();
        for case in golden["valid"].as_array().unwrap() {
            let method = case["method"].as_str().unwrap();
            commands::validate_request(method, &case["request"])
                .unwrap_or_else(|e| panic!("{method}: {e}"));
            commands::validate_response(method, &case["request"], &case["response"])
                .unwrap_or_else(|e| panic!("{method}: {e}"));
        }
        for case in golden["invalid"].as_array().unwrap() {
            assert!(
                commands::validate_request(case["method"].as_str().unwrap(), &case["request"])
                    .is_err()
            );
        }
    }

    #[test]
    fn nested_null_evidence_is_rejected_and_optional_defaults_match_csharp() {
        let golden: Value =
            serde_json::from_slice(include_bytes!("../../../contracts/commands.golden.json"))
                .unwrap();
        let case = golden["valid"]
            .as_array()
            .unwrap()
            .iter()
            .find(|case| case["method"] == "open_project")
            .unwrap();
        let mut response = case["response"].clone();
        response["project"]["frames"] = serde_json::json!([null]);
        assert!(commands::validate_response("open_project", &case["request"], &response).is_err());
        let request: commands::MetadataPageRequest = serde_json::from_value(
            serde_json::json!({"recording_id":"golden","revision":0,"kind":"frames","offset":0}),
        )
        .unwrap();
        assert_eq!(request.limit, 128);
        assert!(
            !serde_json::from_value::<commands::HelloRequest>(serde_json::json!({}))
                .unwrap()
                .notifications
        );
    }

    #[test]
    fn every_required_field_is_enforced_including_nullable_fields() {
        let schema: Value =
            serde_json::from_slice(include_bytes!("../../../contracts/commands.schema.json"))
                .unwrap();
        let golden: Value =
            serde_json::from_slice(include_bytes!("../../../contracts/commands.golden.json"))
                .unwrap();
        for case in golden["valid"].as_array().unwrap() {
            let method = case["method"].as_str().unwrap();
            let contract = &schema["x-methods"][method];
            for field in schema["$defs"][contract["request"].as_str().unwrap()]["required"]
                .as_array()
                .unwrap()
            {
                let mut request = case["request"].clone();
                request
                    .as_object_mut()
                    .unwrap()
                    .remove(field.as_str().unwrap());
                assert!(
                    commands::validate_request(method, &request).is_err(),
                    "{method}: {field}"
                );
            }
            if let Some(shape) = schema["$defs"].get(contract["response"].as_str().unwrap()) {
                for field in shape["required"].as_array().unwrap() {
                    let mut response = case["response"].clone();
                    response
                        .as_object_mut()
                        .unwrap()
                        .remove(field.as_str().unwrap());
                    assert!(
                        commands::validate_response(method, &case["request"], &response).is_err(),
                        "{method}: {field}"
                    );
                }
            }
        }
    }
}
