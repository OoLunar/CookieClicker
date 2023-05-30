use api::discord;

#[macro_use]
extern crate rocket;

mod api {
    pub mod discord;
}

#[rocket::main]
async fn main() -> Result<(), rocket::Error> {
    let _rocket = rocket::build()
        .mount("/api/", discord::routes())
        .launch()
        .await?;
    Ok(())
}
