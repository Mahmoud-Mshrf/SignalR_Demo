# SignalR Demo

An ASP.NET Core 10 sample application with JWT authentication, SQLite persistence, a browser-based private messaging client, and real-time updates using SignalR.

## Features

- Register and sign in with `POST /api/auth/register` and `POST /api/auth/login`.
- Persist users, chats, participants, and messages to SQLite (`signalr_demo.db`). The database is created automatically on startup.
- Send private messages over the authenticated `/hubs/chat` SignalR hub. Messages are saved and delivered to both users' active connections.
- Browse conversations and paginated message history, and mark conversations as read through authenticated REST endpoints.
- Serve the chat interface from the application root (`/`). It supports sign-in, starting conversations by user ID, live messages, unread counts, and reconnecting to the chat hub.
- Broadcast dashboard messages through `/hub/dashboard`. This demonstration hub is currently unauthenticated.

## Requirements and Dependencies

- .NET 10 SDK
- A modern browser
- Internet access when loading the browser client: SignalR JavaScript is loaded from jsDelivr and fonts from Google Fonts
- Optional: VS Code REST Client extension to run the examples in `request.http`
- Optional: Python 3 to serve `chat-test/signaR_test.html` as a standalone test page

The server's NuGet dependencies are `Microsoft.AspNetCore.Authentication.JwtBearer` and `Microsoft.EntityFrameworkCore.Sqlite` (see `SignalR_Demo.csproj`). There is no npm install or separate frontend build step.

## Setup and Run

Run these commands from the repository root:

```powershell
dotnet restore
dotnet build
dotnet run --launch-profile https
```

The HTTPS profile listens on `https://localhost:7061` and `http://localhost:5089`, and opens the application in a browser. Open `https://localhost:7061` for the chat UI. HTTPS redirection is enabled; if the development certificate is not trusted, run:

```powershell
dotnet dev-certs https --trust
```

For HTTP-only local development, use `dotnet run --launch-profile http`; this profile listens on `http://localhost:5089`.

## Configuration

`appsettings.json` contains the SQLite connection string. `appsettings.Development.json` contains local JWT settings, including the signing key, issuer, audience, and 15-minute access-token lifetime. The development signing key is public sample configuration and must not be used in production.

Override settings with .NET environment variables. For example, in PowerShell:

```powershell
$env:JwtSettings__SigningKey = "replace-with-a-long-random-secret"
$env:ConnectionStrings__DefaultConnection = "Data Source=signalr_demo.db"
dotnet run --launch-profile https
```

Configure a strong, private signing key and production-appropriate issuer and audience for deployment. The CORS policy currently allows `http://localhost:5000` for a separately hosted frontend; change the allowed origin in `Program.cs` if using a different frontend origin. The built-in frontend is served from the same origin as the API and does not need a separate frontend server.

## Create Accounts and Sign In

The browser UI currently provides sign-in only. Create accounts using the REST Client requests in `request.http` or another HTTP client. For example:

```http
POST http://localhost:5089/api/auth/register
Content-Type: application/json

{
  "name": "Test User",
  "email": "test.user@example.com",
  "phoneNumber": "+15551234567",
  "password": "ChangeMe123!"
}
```

Then sign in at `POST /api/auth/login` with the same email and password. Both endpoints return HTTP 200 with a response shaped like:

```json
{
  "userId": "<user-guid>",
  "name": "Test User",
  "email": "test.user@example.com",
  "accessToken": "<jwt>"
}
```

Registration requires a name (1-100 characters), valid email, phone number (1-32 characters), and password (8-128 characters). Email addresses are normalized and must be unique. Invalid request data returns HTTP 400, an existing email returns HTTP 409, and invalid login credentials return HTTP 401. In `request.http`, use the returned token for each user in its `@token1` / `@token2` variable; any hard-coded sample token may be expired.

## Use the Chat UI

Sign in at `https://localhost:7061` with an account created above. The access token and user ID are kept in that browser tab's `sessionStorage`; they are removed on sign-out. For a two-user test, sign in as each account in separate browser profiles or in a regular and private window. Copy one user's ID using the **ID** button, then in the other window choose **New conversation**, paste that ID, and send the first message. That message creates the conversation. Select the conversation to load history; new messages, unread counts, and read state update as you use the app.

Messages must contain 1-2,000 characters and cannot be sent to your own user ID. Access tokens expire after the configured lifetime (15 minutes by default); sign in again when the session expires. There is currently no refresh-token endpoint.

## REST API

All chat endpoints require `Authorization: Bearer <accessToken>`.

| Method and route | Purpose |
| --- | --- |
| `POST /api/auth/register` | Create an account and return a JWT |
| `POST /api/auth/login` | Sign in and return a JWT |
| `GET /api/chats?page=1&size=10` | List the signed-in user's chats, newest first, including the last message and unread count |
| `GET /api/chats/{chatId}/messages?page=1&size=10` | Get paginated messages for a chat the user participates in |
| `POST /api/chats/{chatId}/read` | Mark a chat as read; returns HTTP 204 on success |

Paginated responses include `items`, `page`, `pageSize`, `totalCount`, and `hasNextPage`. For example, after logging in and saving the token:

```http
GET http://localhost:5089/api/chats?page=1&size=10
Authorization: Bearer <accessToken>
```

`GET /api/chats/{chatId}/messages` returns HTTP 404 if the chat does not exist and HTTP 403 if the signed-in user is not a participant.

## SignalR Hubs

| Route | Authentication | Methods and events |
| --- | --- | --- |
| `/hubs/chat` | Required | Invoke `SendPrivateMessage(receiverId, content)`; clients receive `ReceiveMessage` with `chatId`, `senderId`, `content`, and `sentAt` |
| `/hub/dashboard` | Not required | Invoke `SendAll(text)`; clients receive `ReceiveText` |
| `/hubs/notifications` | Not required | Endpoint is registered, but no notification-sending method is implemented |

The chat browser client obtains the JWT via `accessTokenFactory`; the server accepts it for hub requests under `/hubs`. The included page loads the SignalR JavaScript client, so the dashboard broadcast can be tested in its developer console:

```javascript
const dashboard = new signalR.HubConnectionBuilder()
  .withUrl("https://localhost:7061/hub/dashboard")
  .build();
dashboard.on("ReceiveText", text => console.log(text));
await dashboard.start();
await dashboard.invoke("SendAll", "Dashboard test");
```

Open the page in another tab and connect there as well to see the broadcast in both consoles. The dashboard hub is intentionally unauthenticated in this demo; protect it before exposing it beyond local testing.

## Data and Test Client

The SQLite database persists between runs. To start over, stop the application and delete `signalr_demo.db`; this permanently removes registered accounts and chat history. The optional low-level SignalR browser test is at `chat-test/signaR_test.html` and can be served locally with Python 3, for example `python -m http.server 8000` from the repository root. The primary UI is served directly by ASP.NET Core.
