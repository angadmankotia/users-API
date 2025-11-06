🧑‍💻 Users API (ASP.NET Core 8)

A simple CRUD Web API built with ASP.NET Core 8, Entity Framework Core, SQLite, and JWT Authentication.

🚀 Features

Create, Read, Update, Delete users

JWT-based login/authentication

SQLite database (auto-created)

Validation & middleware logging

Swagger UI for easy testing

⚙️ Setup
1️⃣ Prerequisites

.NET SDK 8+

VS Code or Visual Studio

2️⃣ Install packages
dotnet add package Microsoft.EntityFrameworkCore.Sqlite
dotnet add package Microsoft.EntityFrameworkCore.Tools
dotnet add package Microsoft.AspNetCore.Authentication.JwtBearer
dotnet add package Swashbuckle.AspNetCore

3️⃣ Run
dotnet restore
dotnet build
dotnet run


Then open:
👉 https://localhost:5242/swagger

🧩 Endpoints
Method	Endpoint	Auth	Description
GET	/health	❌	API status
POST	/login	❌	Get JWT token
GET	/users	❌	Get all users
GET	/users/{id}	❌	Get one user
POST	/users	✅	Add new user
PUT	/users/{id}	✅	Update user
DELETE	/users/{id}	✅	Delete user
🔐 Login Example
POST /login
{
  "email": "alice@example.com",
  "password": "anything"
}


Copy the token → click Authorize 🔒 in Swagger → paste:

Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6...

💾 Database

File: users.db (SQLite)

Seeded data:

Alice (28)

Bob (35)

🛠 Built With

ASP.NET Core 8

Entity Framework Core

SQLite

JWT Auth

Swagger

🧠 Author

Angad Mankotia — Learning ASP.NET Core
