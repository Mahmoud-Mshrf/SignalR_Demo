# SignalR Demo

A small ASP.NET Core 10 application that demonstrates JWT authentication, SQLite persistence, and real-time messaging with SignalR.

## What is implemented

- `POST /api/auth/register` creates an account and returns its user ID and access token.
- `POST /api/auth/login` validates credentials and returns the same response shape.
- The users, chats, chat participants, and messages are stored in SQLite in `signalr_demo.db`. The database is created automatically when the application starts.
- `/hubs/chat` is an authenticated SignalR hub. Its `SendPrivateMessage(receiverId, content)` method persists a message and sends a `ReceiveMessage` event to both users' active connections.
- `/hub/dashboard` provides `SendAll(text)`, which broadcasts a `ReceiveText` event to all connected clients. This hub is not currently protected by authorization.
- `/hubs/notifications` is registered, but currently has no server method that sends notifications.
- `chat-test/signaR_test.html` is a minimal browser client for private chat.

## Requirements

- .NET 10 SDK
- VS Code REST Client extension to run `request.http`
- A modern browser and an internet connection for the SignalR JavaScript client loaded from jsDelivr
- Python 3 only if using the included browser test page through a local static server

## Run the application

From the repository root, build and start the HTTPS launch profile:

```powershell
dotnet build
dotnet run --launch-profile https
```

The HTTPS profile listens on `https://localhost:7061` and `http://localhost:5089`. HTTPS redirection is enabled. If the local HTTPS certificate is not trusted, create/trust a development certificate with `dotnet dev-certs https --trust` and restart the application.

The development configuration is for local testing only. Do not use the development JWT signing key or other development settings in a deployed environment.

## Test registration and login

With the app running, open `request.http` and send the two **Register** requests, then the two **Login** requests. The examples create two accounts with separate email addresses; login returns an `accessToken` and `userId` for each account.

The request file currently points at `http://localhost:5089`. For direct HTTPS requests, set its `@baseUrl` to `https://localhost:7061`. Registration requires a name, valid email, phone number, and password of at least eight characters. Emails must be unique: if an example account already exists, run its Login request instead of registering it again, or change the email.

Successful registration and login return HTTP 200 with a response like:

```json
{
  "userId": "<user-guid>",
  "name": "Test User",
  "email": "test.user@example.com",
  "accessToken": "<jwt>"
}
```

Invalid request data returns HTTP 400. Registering an existing email returns HTTP 409, and invalid login credentials return HTTP 401.

## Test private chat

1. Start the application using the HTTPS profile.
2. In a second terminal, serve the test page from its directory:

   ```powershell
   cd chat-test
   python -m http.server 5000
   ```

3. Open `http://localhost:5000/signaR_test.html` in two browser tabs. The server allows this origin for SignalR connections.
4. In the first tab, paste the first account's `accessToken` and connect. In the second tab, paste the other account's token and connect.
5. In each tab, enter the *other* account's `userId` as the receiver ID. Send a message from either tab; it should appear in both connected clients.

The chat hub requires a valid JWT. Messages must be non-empty, no longer than 2,000 characters, and cannot be sent to the sender's own account. Access tokens expire after the configured development lifetime, currently 15 minutes; log in again to obtain another token.

## Test the dashboard broadcast

The included chat page loads the SignalR JavaScript client, so its browser developer console can also make a dashboard connection. On that page, run:

```javascript
const dashboard = new signalR.HubConnectionBuilder()
  .withUrl("https://localhost:7061/hub/dashboard")
  .build();
dashboard.on("ReceiveText", text => console.log(text));
await dashboard.start();
await dashboard.invoke("SendAll", "Dashboard test");
```

Open the page in another tab and connect there too to observe the broadcast in both consoles. The dashboard hub currently accepts unauthenticated connections.

## Hub routes

| Route | Authentication | Current functionality |
| --- | --- | --- |
| `/hubs/chat` | Required | `SendPrivateMessage(receiverId, content)`; receives `ReceiveMessage` events |
| `/hub/dashboard` | Not required | `SendAll(text)`; receives `ReceiveText` events |
| `/hubs/notifications` | Not required | Hub endpoint is registered; no notification-sending method is implemented yet |

The SQLite database persists between runs. Use new email addresses when you need fresh accounts; deleting the database also deletes the saved users and chat history.