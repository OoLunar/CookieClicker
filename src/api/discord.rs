use actix_web::{
    dev::Payload,
    error::PayloadError,
    http::StatusCode,
    web::{Bytes, Json},
    Either, FromRequest, HttpRequest, Responder, ResponseError,
};

use ed25519_compact::PublicKey;
use futures::{Future, StreamExt, TryStreamExt};
use serde::Deserialize;
use std::{fmt::Debug, pin::Pin};
use twilight_model::{
    application::interaction::{Interaction, InteractionType},
    http::interaction::{InteractionResponse, InteractionResponseType},
};

use derive_more::{Display, Error};

#[derive(Debug, Display, Error)]
enum DiscordHeaderError {
    #[display(fmt = "Missing one of two required Ed25519 signature headers.")]
    MissingHeader,

    #[display(fmt = "Invalid Ed25519 signature.")]
    InvalidHeader,

    #[display(fmt = "Missing body.")]
    MissingBody,

    #[display(fmt = "Invalid JSON body.")]
    InvalidBody,
}

impl ResponseError for DiscordHeaderError {
    fn status_code(&self) -> StatusCode {
        StatusCode::BAD_REQUEST
    }
}

struct DiscordSigned<T> {
    pub body: Json<T>,
    pub signature: String,
    pub timestamp: String,
}

impl<T: for<'de> Deserialize<'de>> FromRequest for DiscordSigned<T> {
    type Error = DiscordHeaderError;
    type Future = Pin<Box<dyn Future<Output = Result<DiscordSigned<T>, DiscordHeaderError>>>>;

    fn from_request(req: &HttpRequest, payload: &mut Payload) -> Self::Future {
        let signature = req.headers().get("X-Signature-Ed25519");
        let timestamp = req.headers().get("X-Signature-Timestamp");
        let content = req.headers().get("Content-Length");
        let content_len = match content.and_then(|c| c.to_str().ok()?.parse::<usize>().ok()) {
            Some(len) => len,
            None => return Box::pin(async { Err(DiscordHeaderError::MissingHeader) }),
        };

        if content_len == 0 {
            return Box::pin(async { Err(DiscordHeaderError::MissingBody) });
        }

        Box::pin(async move {
            let collection = payload.left_stream().into_future().await;
            if collection.0.iter().any(|result| result.is_err()) {
                return Err(DiscordHeaderError::InvalidHeader);
            }

            let full_body: Vec<u8> = collection
                .0
                .iter()
                .map(|result| result.unwrap().to_vec())
                .flatten()
                .collect();

            let result = PublicKey::from_slice(todo!()).unwrap().verify(
                &full_body,
                &ed25519_compact::Signature::from_slice(signature.unwrap().as_bytes()).unwrap(),
            );

            if result.is_err() {
                return Err(DiscordHeaderError::InvalidHeader);
            }

            let body = Json::<T>::from_request(req, payload).await;
            if body.is_err() {
                return Err(DiscordHeaderError::InvalidBody);
            }

            Ok(DiscordSigned {
                body: body.unwrap(),
                signature: signature.unwrap().to_str().unwrap().to_string(),
                timestamp: timestamp.unwrap().to_str().unwrap().to_string(),
            })
        })
    }
}

#[post("/api/discord")]
async fn ping(interaction: DiscordSigned<Interaction>) -> impl Responder {
    match interaction.body.kind {
        InteractionType::Ping => {
            return (
                StatusCode::OK,
                Json(InteractionResponse {
                    kind: InteractionResponseType::Pong,
                    data: None,
                }),
            )
        }
        _ => unimplemented!("Interaction type not implemented"),
    };
}
