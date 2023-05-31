use std::fmt::Display;

use actix_web::{
    dev::ServiceResponse, http::header, middleware::ErrorHandlerResponse, web, App, HttpResponse,
    HttpServer, Responder, Result,
};
use api::discord;
use serde_json::json;

mod api {
    pub mod discord;
}

#[actix_web::main]
async fn main() -> Result<()> {
    HttpServer::new(|| App::new().route("/", web::get().to(manual_hello)))
        .bind(("0.0.0.0", 8080))?
        .run()
        .await
}

async fn manual_hello() -> impl Responder {
    HttpResponse::Ok().body("Hello, world!")
}

fn add_error_header<B>(mut res: ServiceResponse<B>) -> Result<ErrorHandlerResponse<String>>
where
    B: Display,
{
    if !res.status().is_success() {
        // TODO: Log
        let mutable = res.response_mut();
        mutable.headers_mut().insert(header::CONTENT_TYPE, mime);
        mutable.set_body(json!({"error": "{}"}).fmt(mutable.body()));
    }

    Ok(ErrorHandlerResponse::Response(res.map_into_left_body()))
}
