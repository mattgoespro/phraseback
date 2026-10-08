// Generated from contracts/protocol.schema.json. Do not edit by hand.

#[derive(Debug, Deserialize, Serialize)]
#[serde(deny_unknown_fields)]
pub struct Request {
    pub protocol: u32,
    pub id: u64,
    pub method: String,
    pub params: Value,
}

#[derive(Debug, Deserialize, Serialize)]
#[serde(deny_unknown_fields)]
pub struct Response {
    pub protocol: u32,
    pub id: u64,
    pub session: String,
    pub result: Option<Value>,
    pub error: Option<Error>,
}

#[derive(Debug, Deserialize, Serialize)]
#[serde(deny_unknown_fields)]
pub struct Notification {
    pub protocol: u32,
    pub session: String,
    pub event: String,
    pub data: Value,
}

#[derive(Debug, Deserialize, Serialize)]
#[serde(deny_unknown_fields)]
pub struct Error {
    pub code: String,
    pub message: String,
}
