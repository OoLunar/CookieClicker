use std::fmt::Debug;

use rocket::{
    data::{FromData, Outcome, ToByteUnit},
    http::Status,
    serde::json::Json,
    Data, Request, Route,
};
use serde::de::DeserializeOwned;
use twilight_model::{
    application::interaction::{Interaction, InteractionType},
    http::interaction::{InteractionResponse, InteractionResponseType},
};

#[derive(Debug)]
enum DiscordHeaderError {
    MissingHeader,
    InvalidHeader,
    MissingBody,
}

struct DiscordSigned<'r, T> {
    pub body: Json<T>,
    pub signature: &'r str,
    pub timestamp: &'r str,
}

#[rocket::async_trait]
impl<'r, T: DeserializeOwned + Send> FromData<'r> for DiscordSigned<'r, T> {
    type Error = DiscordHeaderError;

    async fn from_data(req: &'r Request<'_>, data: Data<'r>) -> Outcome<'r, Self> {
        let signature = req.headers().get_one("X-Signature-Ed25519");
        let timestamp = req.headers().get_one("X-Signature-Timestamp");
        if signature.is_none() || timestamp.is_none() {
            return Outcome::Failure((Status::BadRequest, DiscordHeaderError::MissingHeader));
        }

        let body = Json::<T>::from_data(req, data).await.unwrap() else {
            return Outcome::Failure((Status::BadRequest, DiscordHeaderError::MissingBody));
        };

        let rawBody = data.open(8.megabytes()).into_bytes().await.unwrap();

        let result = ed25519_compact::PublicKey::from_slice(todo!())
            .unwrap()
            .verify(
                vec![timestamp.unwrap().as_bytes(), rawBody.leak()].concat(),
                &ed25519_compact::Signature::from_slice(signature.unwrap().as_bytes()).unwrap(),
            );

        return match result {
            Err(_) => Outcome::Failure((Status::BadRequest, DiscordHeaderError::InvalidHeader)),
            Ok(_) => Outcome::Success(DiscordSigned {
                body,
                signature: signature.unwrap(),
                timestamp: timestamp.unwrap(),
            }),
        };
    }
}

#[post("/discord", format = "json", data = "<interaction>")]
fn ping(interaction: DiscordSigned<Interaction>) -> Json<InteractionResponse> {
    return Json(match interaction.body.kind {
        InteractionType::Ping => InteractionResponse {
            kind: InteractionResponseType::Pong,
            data: None,
        },
        _ => unimplemented!("Interaction type not implemented"),
    });
}

pub fn routes() -> Vec<Route> {
    routes![ping]
}
