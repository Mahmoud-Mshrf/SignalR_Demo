# SignalR Demo

An ASP.NET Core 10 messaging application with JWT authentication, SQLite persistence, browser-based direct and group conversations, and real-time updates using SignalR.

## Features

- Register and sign in in the browser or with `POST /api/auth/register` and `POST /api/auth/login`.
- Persist users, direct chats, groups, participants, and messages to SQLite (`signalr_demo.db`). A new database schema is created automatically on startup.
- Send private and group messages over the authenticated `/hubs/chat` SignalR hub. Messages are persisted before being broadcast.
- Browse paginated direct-chat history and group history, view group members, and mark conversations as read through authenticated REST endpoints.
- Create groups and manage members in the browser. The creator is the group admin; only the admin can add or remove members and inspect the full user roster.
- Receive real-time group-added and group-removed notifications. Active sessions join or leave the corresponding SignalR room without requiring a page refresh.
- See persistent unread counts for direct chats and groups, including messages received while offline. Opening a conversation marks it as read.
- Serve the chat interface from the application root (`/`). It includes registration, sign-in, direct and group conversations, member management, and reconnecting to the chat hub.
- Send dashboard-wide text with `/hub/dashboard`. This hub is restricted to users with the `Manager` or `SuperAdmin` role.

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

`appsettings.json` contains the SQLite connection string. `appsettings.Development.json` contains local JWT settings, including the signing key, issuer, audience, and access-token lifetime. The development signing key is public sample configuration and must not be used in production.

Override settings with .NET environment variables. For example, in PowerShell:

```powershell
$env:JwtSettings__SigningKey = "replace-with-a-long-random-secret"
$env:ConnectionStrings__DefaultConnection = "Data Source=signalr_demo.db"
dotnet run --launch-profile https
```

Configure a strong, private signing key and production-appropriate issuer and audience for deployment. The CORS policy currently allows `http://localhost:5000` for a separately hosted frontend; change the allowed origin in `Program.cs` if using a different frontend origin. The built-in frontend is served from the same origin as the API and does not need a separate frontend server.

The application currently calls EF Core `EnsureCreated` at startup. This creates tables for a new database but does not migrate an existing database when the model changes. Use EF Core migrations for databases that need schema upgrades.

## Accounts and Sign In

Create an account from the browser's sign-in screen, or use the REST Client requests in `request.http`. For example:

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

Registration returns HTTP 200 with a response shaped like:

```json
{
  "userId": "<user-guid>",
  "name": "Test User",
  "email": "test.user@example.com",
  "accessToken": "<jwt>"
}
```

Sign in at `POST /api/auth/login` with the same email and password. Registration requires a name (1-100 characters), valid email, phone number (1-32 characters), and password (8-128 characters). Email addresses are normalized and must be unique. Invalid request data returns HTTP 400, an existing email returns HTTP 409, and invalid login credentials return HTTP 401. In `request.http`, use the returned token for each user in its `@token1` / `@token2` variable; any hard-coded sample token may be expired.

## Use the Chat UI

Sign in at `https://localhost:7061` with an account created above. The access token and user ID are kept in that browser tab's `sessionStorage`; they are removed on sign-out. For a two-user test, sign in as each account in separate browser profiles or in regular and private windows.

For a direct conversation, copy one user's ID with the **ID** button. In the other account, choose **New conversation**, paste that ID, and send the first message. The first message creates the chat.

For a group conversation, choose **Create a group**. The creator is its admin. Use **Manage** to see all users and add or remove members; all group members can use **Members** to see the joined users. A newly added online user receives a notification and joins the group without refreshing. Removed users lose access immediately. Group unread badges include messages received while offline and clear when the group is opened.

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
| `GET /api/groups` | List groups the signed-in user joined, including admin status and unread count |
| `POST /api/groups` | Create a group; the signed-in user becomes its admin |
| `GET /api/groups/{groupId}/members` | List joined members; available to group members |
| `GET /api/groups/{groupId}/messages` | Get message history; available to group members |
| `POST /api/groups/{groupId}/read` | Mark group messages as read; returns HTTP 204 |
| `GET /api/groups/{groupId}/users` | Admin-only roster of all users, including each user's joined status |
| `POST /api/groups/{groupId}/members` | Admin-only add-member operation |
| `DELETE /api/groups/{groupId}/members/{participantId}` | Admin-only remove-member operation |

Chat list and history endpoints are paginated, accept page sizes from 1 to 100, and return `items`, `page`, `pageSize`, `totalCount`, and `hasNextPage`. Group history currently returns all messages in chronological order. For example, after logging in and saving the token:

```http
GET http://localhost:5089/api/chats?page=1&size=10
Authorization: Bearer <accessToken>
```

Endpoints require a JWT. Group creation and membership operations use the authenticated user's ID from their token; clients cannot select a manager ID. Invalid request data returns HTTP 400, missing resources return HTTP 404, nonmember or nonadmin access returns HTTP 403, and duplicate add-member requests return HTTP 409. Error responses use Problem Details.

## SignalR Hubs

| Route | Authentication | Methods and events |
| --- | --- | --- |
| `/hubs/chat` | Required | Invoke `SendPrivateMessage(receiverId, content)`, `SendGroupMessage(content, groupId)`, or `JoinRoom(groupId)`; clients receive `ReceiveMessage`, `GroupAdded`, and `GroupRemoved` |
| `/hub/dashboard` | Manager or SuperAdmin role | Invoke `SendAll(text)`; connected clients receive `ReceiveText` |
| `/hubs/notifications` | Not required | Endpoint is registered, but no notification-sending method is implemented |

The chat browser client obtains its JWT via `accessTokenFactory`; the server accepts hub tokens under `/hubs`. On connection, the server joins the user's existing groups. `JoinRoom` verifies membership before joining, and the sender ID for group messages comes from the authenticated connection. Group membership changes notify all of the affected user's active connections. The dashboard broadcast can be tested with a manager or superadmin token:

```javascript
const dashboard = new signalR.HubConnectionBuilder()
  .withUrl("https://localhost:7061/hub/dashboard", {
    accessTokenFactory: () => accessToken
  })
  .build();
dashboard.on("ReceiveText", text => console.log(text));
await dashboard.start();
await dashboard.invoke("SendAll", "Dashboard test");
```

Open the page in another tab and connect there as well to see the broadcast in both consoles. The dashboard hub is restricted to users whose JWT role is `Manager` or `SuperAdmin`.

## Data and Test Client

The SQLite database persists between runs. To start over, stop the application and delete `signalr_demo.db`; this permanently removes registered accounts and chat history. The optional low-level SignalR browser test is at `chat-test/signaR_test.html` and can be served locally with Python 3, for example `python -m http.server 8000` from the repository root. The primary UI is served directly by ASP.NET Core.
