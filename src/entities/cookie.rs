use std::hash::Hash;

use serde::Deserialize;
use ulid::Ulid;

#[derive(Clone, Copy, Debug, Default, Deserialize, Hash)]
pub struct Cookie {
    pub id: Ulid,
    pub clicks: u128,
}

impl Cookie {
    pub fn new() -> Self {
        Self {
            id: Ulid::new(),
            clicks: 0,
        }
    }
}
