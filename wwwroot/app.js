(function () {
    "use strict";

    const app = document.getElementById("app");
    const toastRegion = document.getElementById("toast-region");
    const sessionKey = "signalr-demo-session";
    const apiBaseUrl = window.location.origin;
    const guidPattern = /^[0-9a-f]{8}-[0-9a-f]{4}-[1-8][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i;

    let session = readSession();
    let connection = null;
    let connectionStartPromise = null;
    let chats = [];
    let groups = [];
    let activeList = "direct";
    let currentChatId = null;
    let currentGroupId = null;
    let historyReadyChatId = null;
    let pendingMessages = new Map();
    let renderedMessageKeys = new Set();
    let groupMemberNames = new Map();
    let chatRefreshVersion = 0;
    let conversationVersion = 0;
    let pendingNewConversation = null;
    let authMode = "login";

    function readSession() {
        try {
            const saved = JSON.parse(sessionStorage.getItem(sessionKey) || "null");
            return saved && saved.accessToken && saved.userId ? saved : null;
        } catch {
            return null;
        }
    }

    function escapeHtml(value) {
        return String(value ?? "").replace(/[&<>"']/g, character => ({
            "&": "&amp;",
            "<": "&lt;",
            ">": "&gt;",
            '"': "&quot;",
            "'": "&#39;"
        })[character]);
    }

    function initials(name) {
        return String(name || "?")
            .trim()
            .split(/\s+/)
            .slice(0, 2)
            .map(part => part.charAt(0))
            .join("")
            .toUpperCase() || "?";
    }

    function formatTime(value) {
        if (!value) return "";
        const date = new Date(value);
        if (Number.isNaN(date.getTime())) return "";
        const today = new Date();
        if (date.toDateString() === today.toDateString()) {
            return new Intl.DateTimeFormat(undefined, { hour: "numeric", minute: "2-digit" }).format(date);
        }
        return new Intl.DateTimeFormat(undefined, { month: "short", day: "numeric" }).format(date);
    }

    function formatMessageTime(value) {
        const date = new Date(value);
        return Number.isNaN(date.getTime())
            ? ""
            : new Intl.DateTimeFormat(undefined, { hour: "numeric", minute: "2-digit" }).format(date);
    }

    function parseRoute() {
        const match = window.location.pathname.match(/^\/(chat|group)\/([^/]+)\/?$/i);
        if (!match || !guidPattern.test(match[2])) return null;
        return { type: match[1].toLowerCase(), id: match[2].toLowerCase() };
    }

    function navigate(path) {
        if (window.location.pathname !== path) {
            window.history.pushState({}, "", path);
        }
        void renderRoute();
    }

    async function apiRequest(path, options = {}) {
        const headers = new Headers(options.headers || {});
        if (session && !headers.has("Authorization")) {
            headers.set("Authorization", `Bearer ${session.accessToken}`);
        }
        if (options.body && !headers.has("Content-Type")) {
            headers.set("Content-Type", "application/json");
        }

        let response;
        try {
            response = await fetch(`${apiBaseUrl}${path}`, { ...options, headers });
        } catch {
            throw new Error("Could not reach the server. Check that the application is running.");
        }

        if (response.status === 401 && session) {
            void endSession("Your session has expired. Sign in again.");
            throw new Error("Your session has expired. Sign in again.");
        }

        if (response.status === 204) return null;
        const result = await response.json().catch(() => null);
        if (!response.ok) {
            throw new Error(result?.title || result?.detail || `The request failed (${response.status}).`);
        }
        return result;
    }

    function renderLogin(errorMessage = "") {
        const registering = authMode === "register";
        app.innerHTML = `
            <main class="login-page">
                <aside class="login-aside">
                    <div class="brand-lockup"><span class="brand-mark">S</span><span>SignalR Demo</span></div>
                    <div class="login-aside-copy">
                        <p class="eyebrow">PRIVATE MESSAGES / REAL TIME</p>
                        <h1>Good conversations<br><span>move things forward.</span></h1>
                        <p class="login-aside-note">A quieter place for the messages that matter.</p>
                    </div>
                    <div class="login-aside-footer">Secure sign-in with your account</div>
                </aside>
                <section class="login-main">
                    <div class="login-form-wrap">
                        <p class="section-kicker">${registering ? "NEW ACCOUNT" : "WELCOME BACK"}</p>
                        <h2>${registering ? "Create your account" : "Sign in"}</h2>
                        <p class="login-subtitle">${registering ? "Join your conversations in one place." : "Use your SignalR Demo account."}</p>
                        <form id="login-form" class="login-form">
                            ${registering ? `
                            <div class="field">
                                <label for="name">Name</label>
                                <input id="name" name="name" autocomplete="name" required maxlength="100" placeholder="Your name">
                            </div>
                            <div class="field">
                                <label for="phone">Phone number</label>
                                <input id="phone" name="phoneNumber" type="tel" autocomplete="tel" required maxlength="32" placeholder="+1 555 123 4567">
                            </div>` : ""}
                            <div class="field">
                                <label for="email">Email</label>
                                <input id="email" name="email" type="email" autocomplete="username" required maxlength="320" placeholder="you@example.com">
                            </div>
                            <div class="field">
                                <label for="password">Password</label>
                                <input id="password" name="password" type="password" autocomplete="current-password" required maxlength="128" placeholder="Your password">
                            </div>
                            <p id="auth-error" class="auth-error" role="alert">${escapeHtml(errorMessage)}</p>
                            <button id="login-button" class="primary-button" type="submit">${registering ? "Create account" : "Sign in"} <span aria-hidden="true">→</span></button>
                        </form>
                        <button id="auth-mode-toggle" class="auth-mode-toggle" type="button">${registering ? "Already have an account? Sign in" : "New here? Create an account"}</button>
                    </div>
                </section>
            </main>`;

        document.getElementById("login-form").addEventListener("submit", handleAuthentication);
        document.getElementById("auth-mode-toggle").addEventListener("click", () => {
            authMode = registering ? "login" : "register";
            renderLogin();
        });
    }

    async function handleAuthentication(event) {
        event.preventDefault();
        const form = event.currentTarget;
        const button = document.getElementById("login-button");
        const error = document.getElementById("auth-error");
        button.disabled = true;
        error.textContent = "";

        try {
            const registering = authMode === "register";
            const response = await apiRequest(registering ? "/api/auth/register" : "/api/auth/login", {
                method: "POST",
                body: JSON.stringify(registering ? {
                    name: form.elements.name.value.trim(),
                    email: form.elements.email.value.trim(),
                    phoneNumber: form.elements.phoneNumber.value.trim(),
                    password: form.elements.password.value
                } : {
                    email: form.elements.email.value.trim(),
                    password: form.elements.password.value
                })
            });
            session = {
                accessToken: response.accessToken,
                userId: response.userId,
                name: response.name,
                email: response.email
            };
            sessionStorage.setItem(sessionKey, JSON.stringify(session));
            chats = [];
            groups = [];
            await enterApplication();
        } catch (requestError) {
            error.textContent = requestError.message || "Sign in failed. Check your details and try again.";
        } finally {
            if (button.isConnected) button.disabled = false;
        }
    }

    async function endSession(message = "") {
        const activeConnection = connection;
        connection = null;
        session = null;
        chats = [];
        currentChatId = null;
        sessionStorage.removeItem(sessionKey);
        if (activeConnection && activeConnection.state !== signalR.HubConnectionState.Disconnected) {
            await activeConnection.stop().catch(() => {});
        }
        renderLogin(message);
    }

    async function enterApplication() {
        renderShell();
        void connectHub();
        await Promise.all([
            refreshChats().catch(error => showToast("Chats could not be loaded", error.message)),
            refreshGroups().catch(error => showToast("Groups could not be loaded", error.message))
        ]);
        await renderRoute();
    }

    function renderShell() {
        app.innerHTML = `
            <div class="app-shell">
                <header class="topbar">
                    <a class="brand-lockup" href="/" data-home aria-label="SignalR Demo home"><span class="brand-mark">S</span><span>SignalR Demo</span></a>
                    <div class="topbar-actions">
                        <button id="connection-status" class="connection-status" data-state="connecting" type="button" title="Click to connect or reconnect"><span>Connecting</span></button>
                        <div class="user-identity">
                            <button id="copy-user-id" class="icon-button" type="button" title="Copy your user ID" aria-label="Copy your user ID">ID</button>
                            <span class="user-name">${escapeHtml(session.name)}</span>
                            <button id="sign-out" class="plain-button" type="button">Sign out</button>
                        </div>
                    </div>
                </header>
                <div id="workspace" class="workspace">
                    <aside class="sidebar">
                        <div class="sidebar-heading">
                            <div><p class="section-kicker">YOUR SPACE</p><h1>Messages</h1></div>
                            <div class="sidebar-actions">
                                <button id="open-create-group" class="new-chat-button group-create-button" type="button" title="Create a group" aria-label="Create a group">▦</button>
                                <button id="open-new-chat" class="new-chat-button" type="button" title="Start a direct conversation" aria-label="Start a direct conversation">+</button>
                            </div>
                        </div>
                        <div class="profile-strip">
                            <span class="avatar">${escapeHtml(initials(session.name))}</span>
                            <div class="profile-copy"><strong>${escapeHtml(session.name)}</strong><span>${escapeHtml(session.email)}</span></div>
                        </div>
                        <div class="conversation-tabs" role="tablist" aria-label="Conversation type">
                            <button class="conversation-tab is-active" type="button" role="tab" aria-selected="true" data-list="direct">Direct</button>
                            <button class="conversation-tab" type="button" role="tab" aria-selected="false" data-list="groups">Groups</button>
                        </div>
                        <div class="list-label"><span id="list-label-text">DIRECT MESSAGES</span><span id="chat-count">0</span></div>
                        <nav id="chat-list" class="chat-list" aria-label="Conversations"></nav>
                    </aside>
                    <main id="conversation" class="conversation"></main>
                </div>
            </div>
            <dialog id="new-chat-dialog" class="modal">
                <form id="new-chat-form" class="modal-content">
                    <div class="modal-heading">
                        <div><p class="section-kicker">NEW MESSAGE</p><h2>Start a conversation</h2></div>
                        <button id="close-new-chat" class="dialog-close" type="button" aria-label="Close">×</button>
                    </div>
                    <div class="field">
                        <label for="recipient-id">Recipient user ID</label>
                        <input id="recipient-id" name="receiverId" required autocomplete="off" spellcheck="false" placeholder="Paste their ID">
                        <small>Copy the other user's ID from their signed-in chat window.</small>
                    </div>
                    <div class="field">
                        <label for="first-message">Message</label>
                        <textarea id="first-message" name="content" required maxlength="2000" rows="3" placeholder="Write your first message"></textarea>
                    </div>
                    <p id="new-chat-error" class="form-error" role="alert"></p>
                    <button id="start-chat-button" class="primary-button" type="submit">Send first message <span aria-hidden="true">→</span></button>
                </form>
            </dialog>`;

        app.insertAdjacentHTML("beforeend", `
            <dialog id="create-group-dialog" class="modal">
                <form id="create-group-form" class="modal-content">
                    <div class="modal-heading">
                        <div><p class="section-kicker">NEW GROUP</p><h2>Create a group</h2></div>
                        <button class="dialog-close" type="button" data-close-dialog="create-group-dialog" aria-label="Close">×</button>
                    </div>
                    <div class="field">
                        <label for="group-name">Group name</label>
                        <input id="group-name" name="groupName" required maxlength="100" placeholder="Project team">
                    </div>
                    <p id="create-group-error" class="form-error" role="alert"></p>
                    <button id="create-group-submit" class="primary-button" type="submit">Create group <span aria-hidden="true">→</span></button>
                </form>
            </dialog>
            <dialog id="members-dialog" class="modal members-modal">
                <section class="modal-content">
                    <div class="modal-heading">
                        <div><p class="section-kicker">GROUP ROSTER</p><h2 id="members-title">Members</h2></div>
                        <button class="dialog-close" type="button" data-close-dialog="members-dialog" aria-label="Close">×</button>
                    </div>
                    <div id="members-list" class="members-list"></div>
                    <p id="members-error" class="form-error" role="alert"></p>
                </section>
            </dialog>`);

        document.querySelectorAll("[data-home]").forEach(link => {
            link.addEventListener("click", event => {
                event.preventDefault();
                navigate("/");
            });
        });
        document.getElementById("sign-out").addEventListener("click", () => void endSession());
        document.getElementById("connection-status").addEventListener("click", () => void connectHub());
        document.getElementById("copy-user-id").addEventListener("click", copyUserId);
        document.getElementById("open-new-chat").addEventListener("click", () => {
            document.getElementById("new-chat-error").textContent = "";
            document.getElementById("new-chat-dialog").showModal();
            document.getElementById("recipient-id").focus();
        });
        document.getElementById("close-new-chat").addEventListener("click", () => {
            document.getElementById("new-chat-dialog").close();
        });
        document.getElementById("new-chat-form").addEventListener("submit", handleStartConversation);
        document.getElementById("open-create-group").addEventListener("click", () => {
            document.getElementById("create-group-error").textContent = "";
            document.getElementById("create-group-dialog").showModal();
            document.getElementById("group-name").focus();
        });
        document.getElementById("create-group-form").addEventListener("submit", handleCreateGroup);
        document.querySelectorAll("[data-close-dialog]").forEach(button => {
            button.addEventListener("click", () => document.getElementById(button.dataset.closeDialog).close());
        });
        document.querySelectorAll("[data-list]").forEach(button => {
            button.addEventListener("click", () => {
                activeList = button.dataset.list;
                document.querySelectorAll("[data-list]").forEach(tab => {
                    const selected = tab === button;
                    tab.classList.toggle("is-active", selected);
                    tab.setAttribute("aria-selected", String(selected));
                });
                renderChatList();
            });
        });
    }

    async function copyUserId() {
        try {
            await navigator.clipboard.writeText(session.userId);
            showToast("User ID copied", "Share it with the person you want to message.");
        } catch {
            showToast("Could not copy ID", "Your user ID is " + session.userId);
        }
    }

    async function handleStartConversation(event) {
        event.preventDefault();
        const form = event.currentTarget;
        const button = document.getElementById("start-chat-button");
        const error = document.getElementById("new-chat-error");
        const receiverId = form.elements.receiverId.value.trim();
        const content = form.elements.content.value.trim();
        error.textContent = "";

        if (!guidPattern.test(receiverId)) {
            error.textContent = "Enter a valid user ID.";
            return;
        }
        if (receiverId.toLowerCase() === session.userId.toLowerCase()) {
            error.textContent = "Choose another user to start a conversation.";
            return;
        }
        if (!content || content.length > 2000) {
            error.textContent = "Enter a message of up to 2,000 characters.";
            return;
        }

        button.disabled = true;
        pendingNewConversation = { receiverId: receiverId.toLowerCase() };
        try {
            await ensureHubConnected();
            await connection.invoke("SendPrivateMessage", receiverId, content);
            await refreshChats();
            const chat = chats.find(item =>
                String(item.receiverId).toLowerCase() === receiverId.toLowerCase() &&
                item.sentByMe && item.content === content);
            if (!chat) throw new Error("The message was sent, but the conversation could not be loaded. Refresh the page.");
            document.getElementById("new-chat-dialog").close();
            form.reset();
            navigate(`/chat/${chat.chatId}`);
        } catch (startError) {
            error.textContent = startError.message || "The message could not be sent. Check the user ID and try again.";
        } finally {
            pendingNewConversation = null;
            button.disabled = false;
        }
    }

    async function refreshChats() {
        const version = ++chatRefreshVersion;
        const items = [];
        for (let page = 1; page <= 100; page += 1) {
            const result = await apiRequest(`/api/chats?page=${page}&size=100`);
            items.push(...(result.items || []));
            if (!result.hasNextPage) break;
        }
        if (version !== chatRefreshVersion) return;
        chats = items.sort((left, right) => new Date(right.sentAt) - new Date(left.sentAt));
        renderChatList();
    }

    async function refreshGroups() {
        groups = await apiRequest("/api/groups");
        if (activeList === "groups") renderChatList();
    }

    async function handleCreateGroup(event) {
        event.preventDefault();
        const form = event.currentTarget;
        const button = document.getElementById("create-group-submit");
        const error = document.getElementById("create-group-error");
        error.textContent = "";
        button.disabled = true;

        try {
            const result = await apiRequest("/api/groups", {
                method: "POST",
                body: JSON.stringify({ groupName: form.elements.groupName.value.trim() })
            });
            await refreshGroups();
            document.getElementById("create-group-dialog").close();
            form.reset();
            activeList = "groups";
            document.querySelector('[data-list="groups"]').click();
            await ensureHubConnected();
            await connection.invoke("JoinRoom", result.groupId);
            navigate(`/group/${result.groupId}`);
        } catch (createError) {
            error.textContent = createError.message || "The group could not be created.";
        } finally {
            if (button.isConnected) button.disabled = false;
        }
    }

    function renderChatList() {
        const list = document.getElementById("chat-list");
        if (!list) return;
        const count = document.getElementById("chat-count");
        if (activeList === "groups") {
            document.getElementById("list-label-text").textContent = "YOUR GROUPS";
            count.textContent = String(groups.length);
            if (!groups.length) {
                list.innerHTML = `<div class="chat-list-empty">Groups you join will appear here.</div>`;
                return;
            }
            list.innerHTML = groups.map(group => `
                <button class="chat-item${String(group.groupId).toLowerCase() === currentGroupId ? " is-active" : ""}" type="button" data-group-id="${escapeHtml(group.groupId)}">
                    <span class="avatar group-avatar" aria-hidden="true">▦</span>
                    <span class="chat-copy">
                        <span class="chat-line"><span class="chat-name">${escapeHtml(group.groupName)}</span>${group.isAdmin ? `<span class="role-mark" title="You manage this group">ADMIN</span>` : ""}</span>
                        <span class="chat-preview">${group.isAdmin ? "Managed by you" : "Group conversation"}</span>
                    </span>
                </button>`).join("");
            list.querySelectorAll("[data-group-id]").forEach(button => {
                button.addEventListener("click", () => navigate(`/group/${button.dataset.groupId}`));
            });
            return;
        }
        document.getElementById("list-label-text").textContent = "DIRECT MESSAGES";
        if (count) count.textContent = String(chats.length);
        if (!chats.length) {
            list.innerHTML = `<div class="chat-list-empty">Your conversations will appear here.</div>`;
            return;
        }

        list.innerHTML = chats.map(chat => {
            const name = chat.receiverName || "New conversation";
            const preview = `${chat.sentByMe ? "You: " : ""}${chat.content || ""}`;
            const unread = Number(chat.unreadMessages) || 0;
            return `
                <button class="chat-item${String(chat.chatId).toLowerCase() === currentChatId ? " is-active" : ""}" type="button" data-chat-id="${escapeHtml(chat.chatId)}">
                    <span class="avatar">${escapeHtml(initials(name))}</span>
                    <span class="chat-copy">
                        <span class="chat-line"><span class="chat-name">${escapeHtml(name)}</span><time class="chat-time">${escapeHtml(formatTime(chat.sentAt))}</time></span>
                        <span class="chat-preview">${escapeHtml(preview)}</span>
                    </span>
                    ${unread > 0 ? `<span class="unread-badge" aria-label="${unread} unread messages">${unread > 99 ? "99+" : unread}</span>` : ""}
                </button>`;
        }).join("");

        list.querySelectorAll("[data-chat-id]").forEach(button => {
            button.addEventListener("click", () => navigate(`/chat/${button.dataset.chatId}`));
        });
    }

    async function openGroupMembers(groupId) {
        const group = groups.find(item => String(item.groupId).toLowerCase() === groupId.toLowerCase());
        if (!group?.isAdmin) return;
        const dialog = document.getElementById("members-dialog");
        document.getElementById("members-title").textContent = group.groupName;
        document.getElementById("members-list").innerHTML = `<p class="thread-loading">Loading roster…</p>`;
        document.getElementById("members-error").textContent = "";
        dialog.showModal();

        try {
            const roster = await apiRequest(`/api/groups/${encodeURIComponent(groupId)}/users`);
            renderGroupRoster(groupId, roster);
        } catch (error) {
            document.getElementById("members-error").textContent = error.message || "The roster could not be loaded.";
        }
    }

    function renderGroupRoster(groupId, roster) {
        const list = document.getElementById("members-list");
        list.innerHTML = roster.map(user => `
            <div class="member-row">
                <span class="avatar">${escapeHtml(initials(user.name))}</span>
                <span class="member-name">${escapeHtml(user.name)}${user.id.toLowerCase() === session.userId.toLowerCase() ? " <small>(you)</small>" : ""}</span>
                <span class="member-state${user.isJoined ? " is-joined" : ""}">${user.isJoined ? "Joined" : "Not joined"}</span>
                ${user.isJoined
                    ? (user.id.toLowerCase() !== session.userId.toLowerCase() ? `<button class="member-action" type="button" data-remove-user="${escapeHtml(user.id)}" title="Remove ${escapeHtml(user.name)}" aria-label="Remove ${escapeHtml(user.name)}">−</button>` : `<span class="role-mark">ADMIN</span>`)
                    : `<button class="member-action is-add" type="button" data-add-user="${escapeHtml(user.id)}" title="Add ${escapeHtml(user.name)}" aria-label="Add ${escapeHtml(user.name)}">+</button>`}
            </div>`).join("") || `<p class="thread-empty">No users are available.</p>`;

        list.querySelectorAll("[data-add-user]").forEach(button => {
            button.addEventListener("click", () => void changeGroupMember(groupId, button.dataset.addUser, true));
        });
        list.querySelectorAll("[data-remove-user]").forEach(button => {
            button.addEventListener("click", () => void changeGroupMember(groupId, button.dataset.removeUser, false));
        });
    }

    async function changeGroupMember(groupId, userId, adding) {
        const error = document.getElementById("members-error");
        error.textContent = "";
        try {
            await apiRequest(adding
                ? `/api/groups/${encodeURIComponent(groupId)}/members`
                : `/api/groups/${encodeURIComponent(groupId)}/members/${encodeURIComponent(userId)}`, {
                method: adding ? "POST" : "DELETE",
                ...(adding ? { body: JSON.stringify({ newParticipantId: userId }) } : {})
            });
            const [roster] = await Promise.all([
                apiRequest(`/api/groups/${encodeURIComponent(groupId)}/users`),
                refreshGroups()
            ]);
            renderGroupRoster(groupId, roster);
        } catch (changeError) {
            error.textContent = changeError.message || "The membership could not be updated.";
        }
    }

    async function renderRoute() {
        if (!session) {
            renderLogin();
            return;
        }
        const route = parseRoute();
        if (!route) {
            currentChatId = null;
            currentGroupId = null;
            historyReadyChatId = null;
            document.getElementById("workspace")?.classList.remove("is-chat-open");
            renderChatList();
            renderWelcome();
            return;
        }
        if (route.type === "group") {
            await openGroup(route.id);
            return;
        }
        await openConversation(route.id);
    }

    function renderWelcome() {
        const pane = document.getElementById("conversation");
        if (!pane) return;
        pane.innerHTML = `
            <section class="welcome-pane">
                <span class="welcome-mark" aria-hidden="true">↗</span>
                <h2>Select a conversation</h2>
                <p>Choose a name from your messages to open the conversation.</p>
            </section>`;
    }

    async function openConversation(chatId) {
        const version = ++conversationVersion;
        currentChatId = chatId;
        currentGroupId = null;
        historyReadyChatId = null;
        document.getElementById("workspace")?.classList.add("is-chat-open");
        renderChatList();

        const chat = findChat(chatId);
        const name = chat?.receiverName || "Conversation";
        const pane = document.getElementById("conversation");
        pane.innerHTML = `
            <section class="conversation-view">
                <header class="conversation-header">
                    <button id="back-to-list" class="back-button" type="button" aria-label="Back to messages">←</button>
                    <span class="avatar">${escapeHtml(initials(name))}</span>
                    <div class="conversation-heading"><h2>${escapeHtml(name)}</h2><p>Private conversation</p></div>
                </header>
                <div id="message-list" class="message-list" aria-live="polite" aria-relevant="additions text"><p class="thread-loading">Loading messages…</p></div>
                <form id="message-form" class="composer">
                    <textarea id="message-input" name="content" rows="1" maxlength="2000" required aria-label="Type a message" placeholder="Write a message..." disabled></textarea>
                    <button class="primary-button" type="submit" disabled>Send <span class="send-arrow" aria-hidden="true">↗</span></button>
                </form>
            </section>`;

        document.getElementById("back-to-list").addEventListener("click", () => navigate("/"));
        document.getElementById("message-form").addEventListener("submit", handleSendMessage);

        try {
            const history = await loadHistory(chatId);
            if (version !== conversationVersion || currentChatId !== chatId) return;
            const live = pendingMessages.get(chatId) || [];
            pendingMessages.delete(chatId);
            historyReadyChatId = chatId;
            renderMessages(mergeMessages(history, live), findChat(chatId));
            await markChatAsRead(chatId);
            if (version === conversationVersion && currentChatId === chatId) {
                document.getElementById("message-input").disabled = false;
                document.querySelector("#message-form button[type='submit']").disabled = false;
            }
        } catch (error) {
            if (version !== conversationVersion || currentChatId !== chatId) return;
            const messageList = document.getElementById("message-list");
            if (messageList) messageList.innerHTML = `<p class="thread-error">${escapeHtml(error.message || "Messages could not be loaded.")}</p>`;
        }
    }

    function findChat(chatId) {
        if (!chatId) return undefined;
        return chats.find(chat => String(chat.chatId).toLowerCase() === chatId.toLowerCase());
    }

    async function openGroup(groupId) {
        const group = groups.find(item => String(item.groupId).toLowerCase() === groupId.toLowerCase());
        if (!group) {
            try {
                await refreshGroups();
            } catch (error) {
                showToast("Groups could not be loaded", error.message);
                return;
            }
        }
        const currentGroup = groups.find(item => String(item.groupId).toLowerCase() === groupId.toLowerCase());
        if (!currentGroup) {
            navigate("/");
            showToast("Group unavailable", "This group is not in your membership list.");
            return;
        }

        const version = ++conversationVersion;
        currentChatId = null;
        currentGroupId = groupId;
        historyReadyChatId = null;
        document.getElementById("workspace")?.classList.add("is-chat-open");
        activeList = "groups";
        document.querySelector('[data-list="groups"]')?.click();
        renderChatList();

        const pane = document.getElementById("conversation");
        pane.innerHTML = `
            <section class="conversation-view">
                <header class="conversation-header">
                    <button id="back-to-list" class="back-button" type="button" aria-label="Back to messages">←</button>
                    <span class="avatar group-avatar" aria-hidden="true">▦</span>
                    <div class="conversation-heading"><h2>${escapeHtml(currentGroup.groupName)}</h2><p>Group conversation${currentGroup.isAdmin ? " · Admin" : ""}</p></div>
                    ${currentGroup.isAdmin ? `<button id="manage-group" class="manage-group-button" type="button" title="Manage group members" aria-label="Manage group members">Members</button>` : ""}
                </header>
                <div id="message-list" class="message-list" aria-live="polite" aria-relevant="additions text"><p class="thread-loading">Loading messages…</p></div>
                <form id="message-form" class="composer">
                    <textarea id="message-input" name="content" rows="1" maxlength="4000" required aria-label="Type a group message" placeholder="Message ${escapeHtml(currentGroup.groupName)}…" disabled></textarea>
                    <button class="primary-button" type="submit" disabled>Send <span class="send-arrow" aria-hidden="true">↗</span></button>
                </form>
            </section>`;

        document.getElementById("back-to-list").addEventListener("click", () => navigate("/"));
        document.getElementById("message-form").addEventListener("submit", handleSendMessage);
        document.getElementById("manage-group")?.addEventListener("click", () => void openGroupMembers(groupId));

        try {
            await ensureHubConnected();
            await connection.invoke("JoinRoom", groupId);
            const [history, members] = await Promise.all([
                loadGroupHistory(groupId),
                apiRequest(`/api/groups/${encodeURIComponent(groupId)}/members`)
            ]);
            if (version !== conversationVersion || currentGroupId !== groupId) return;
            groupMemberNames = new Map(members.map(member => [String(member.id).toLowerCase(), member.name]));
            historyReadyChatId = groupId;
            renderMessages(history, { kind: "group", groupId });
            document.getElementById("message-input").disabled = false;
            document.querySelector("#message-form button[type='submit']").disabled = false;
        } catch (error) {
            if (version !== conversationVersion || currentGroupId !== groupId) return;
            document.getElementById("message-list").innerHTML = `<p class="thread-error">${escapeHtml(error.message || "Group messages could not be loaded.")}</p>`;
        }
    }

    async function loadHistory(chatId) {
        const messages = [];
        for (let page = 1; page <= 100; page += 1) {
            const result = await apiRequest(`/api/chats/${encodeURIComponent(chatId)}/messages?page=${page}&size=100`);
            messages.push(...(result.items || []));
            if (!result.hasNextPage) break;
        }
        return messages.sort((left, right) => new Date(left.sentAt) - new Date(right.sentAt));
    }

    function loadGroupHistory(groupId) {
        return apiRequest(`/api/groups/${encodeURIComponent(groupId)}/messages`);
    }

    function mergeMessages(first, second) {
        const unique = new Map();
        [...first, ...second].forEach(message => unique.set(messageKey(message), message));
        return [...unique.values()].sort((left, right) => new Date(left.sentAt) - new Date(right.sentAt));
    }

    function messageKey(message) {
        return `${String(message.senderId).toLowerCase()}|${message.sentAt}|${message.content}`;
    }

    function renderMessages(messages, chat) {
        const list = document.getElementById("message-list");
        if (!list) return;
        list.replaceChildren();
        renderedMessageKeys = new Set();
        if (!messages.length) {
            const empty = document.createElement("p");
            empty.className = "thread-empty";
            empty.textContent = "No messages yet. Say hello.";
            list.append(empty);
            return;
        }
        messages.forEach(message => appendMessage(message, chat, false));
        list.scrollTop = list.scrollHeight;
    }

    function appendMessage(message, chat = findChat(currentChatId), scroll = true) {
        const key = messageKey(message);
        if (renderedMessageKeys.has(key)) return;
        const list = document.getElementById("message-list");
        if (!list) return;
        renderedMessageKeys.add(key);
        list.querySelector(".thread-empty")?.remove();
        const mine = String(message.senderId).toLowerCase() === session.userId.toLowerCase();
        const isGroup = chat?.kind === "group";
        const sender = mine
            ? "You"
            : (isGroup
                ? groupMemberNames.get(String(message.senderId).toLowerCase()) || "Group member"
                : chat?.receiverName || "Message");
        const row = document.createElement("article");
        row.className = `message-row${mine ? " is-mine" : ""}`;
        row.innerHTML = `
            <div class="message-meta"><span class="message-sender">${escapeHtml(sender)}</span><time class="message-time" datetime="${escapeHtml(message.sentAt)}">${escapeHtml(formatMessageTime(message.sentAt))}</time></div>
            <div class="message-bubble">${escapeHtml(message.content)}</div>`;
        list.append(row);
        if (scroll) list.scrollTop = list.scrollHeight;
    }

    async function markChatAsRead(chatId) {
        try {
            await apiRequest(`/api/chats/${encodeURIComponent(chatId)}/read`, { method: "POST" });
            const chat = findChat(chatId);
            if (chat) chat.unreadMessages = 0;
            renderChatList();
            await refreshChats();
        } catch (error) {
            showToast("Chat could not be marked as read", error.message);
        }
    }

    async function handleSendMessage(event) {
        event.preventDefault();
        const chat = findChat(currentChatId);
        const input = document.getElementById("message-input");
        const button = event.currentTarget.querySelector("button[type='submit']");
        const content = input.value.trim();
        if (!currentGroupId && (!chat || !chat.receiverId)) {
            showToast("Conversation unavailable", "Return to messages and open the conversation again.");
            return;
        }
        if (!content) return;
        const maxLength = currentGroupId ? 4000 : 2000;
        if (content.length > maxLength) {
            showToast("Message is too long", `Messages can contain up to ${maxLength.toLocaleString()} characters.`);
            return;
        }

        button.disabled = true;
        try {
            await ensureHubConnected();
            if (currentGroupId) {
                await connection.invoke("SendGroupMessage", content, currentGroupId);
            } else {
                await connection.invoke("SendPrivateMessage", chat.receiverId, content);
            }
            input.value = "";
            input.focus();
        } catch {
            showToast("Message could not be sent", "Check the connection and recipient, then try again.");
        } finally {
            if (button.isConnected) button.disabled = false;
        }
    }

    async function connectHub() {
        if (!session || !window.signalR) {
            setConnectionStatus("disconnected", "SignalR client unavailable");
            return;
        }
        if (connection && connection.state !== signalR.HubConnectionState.Disconnected) return;

        setConnectionStatus("connecting", "Connecting");
        connection = new signalR.HubConnectionBuilder()
            .withUrl(`${apiBaseUrl}/hubs/chat`, { accessTokenFactory: () => session.accessToken })
            .withAutomaticReconnect()
            .build();

        connection.on("ReceiveMessage", handleReceiveMessage);
        connection.onreconnecting(() => setConnectionStatus("reconnecting", "Reconnecting"));
        connection.onreconnected(() => {
            setConnectionStatus("connected", "Connected");
            void refreshChats().catch(error => showToast("Chats could not be updated", error.message));
        });
        connection.onclose(() => setConnectionStatus("disconnected", "Disconnected · retry"));

        try {
            connectionStartPromise = connection.start();
            await connectionStartPromise;
            setConnectionStatus("connected", "Connected");
        } catch {
            setConnectionStatus("disconnected", "Connection failed · retry");
            showToast("Live connection unavailable", "Click the connection status to try again.");
        } finally {
            connectionStartPromise = null;
        }
    }

    async function ensureHubConnected() {
        if (connection && connection.state === signalR.HubConnectionState.Connecting && connectionStartPromise) {
            await connectionStartPromise;
        } else if (!connection || connection.state === signalR.HubConnectionState.Disconnected) {
            await connectHub();
        }
        if (!connection || connection.state !== signalR.HubConnectionState.Connected) {
            throw new Error("The live connection is not available.");
        }
    }

    function setConnectionStatus(state, label) {
        const status = document.getElementById("connection-status");
        if (!status) return;
        status.dataset.state = state;
        status.querySelector("span").textContent = label;
    }

    function handleReceiveMessage(message) {
        const conversationId = message?.chatOrGroupId || message?.chatId;
        if (!conversationId || !message?.senderId) return;
        const chatId = String(conversationId).toLowerCase();
        const isMine = String(message.senderId).toLowerCase() === session.userId.toLowerCase();

        const group = groups.find(item => String(item.groupId).toLowerCase() === chatId);
        if (group) {
            if (currentGroupId === chatId && historyReadyChatId === chatId) {
                appendMessage(message, { kind: "group", groupId: chatId });
            }
            if (!isMine && currentGroupId !== chatId) {
                showToast(group.groupName, message.content || "", () => navigate(`/group/${chatId}`));
            }
            return;
        }

        updateChatPreview(message, !isMine && chatId !== currentChatId);

        if (chatId === currentChatId) {
            if (historyReadyChatId === chatId) {
                appendMessage(message, findChat(chatId));
            } else {
                const queued = pendingMessages.get(chatId) || [];
                queued.push(message);
                pendingMessages.set(chatId, queued);
            }
            if (!isMine) void markChatAsRead(chatId);
        }

        const refresh = refreshChats();
        if (!isMine && chatId !== currentChatId) {
            void refresh.then(() => {
                if (currentChatId === chatId) return;
                const chat = findChat(chatId);
                showToast(chat?.receiverName || "New message", message.content || "", () => navigate(`/chat/${chatId}`));
            }).catch(error => {
                if (currentChatId !== chatId) {
                    showToast("New message", message.content || "", () => navigate(`/chat/${chatId}`));
                }
                showToast("Chats could not be updated", error.message);
            });
        } else {
            void refresh.catch(error => showToast("Chats could not be updated", error.message));
        }
    }

    function updateChatPreview(message, incrementUnread) {
        const chatId = String(message.chatOrGroupId || message.chatId).toLowerCase();
        let chat = findChat(chatId);
        const isMine = String(message.senderId).toLowerCase() === session.userId.toLowerCase();
        if (!chat) {
            chat = {
                chatId,
                receiverId: isMine ? pendingNewConversation?.receiverId || "" : message.senderId,
                receiverName: "New conversation",
                content: "",
                sentAt: message.sentAt,
                sentByMe: false,
                unreadMessages: 0
            };
            chats.unshift(chat);
        }
        chat.content = message.content;
        chat.sentAt = message.sentAt;
        chat.sentByMe = isMine;
        if (incrementUnread) chat.unreadMessages = (Number(chat.unreadMessages) || 0) + 1;
        chats.sort((left, right) => new Date(right.sentAt) - new Date(left.sentAt));
        renderChatList();
    }

    function showToast(title, message, onClick) {
        const toast = document.createElement(onClick ? "button" : "div");
        toast.className = "toast";
        if (onClick) toast.type = "button";
        toast.innerHTML = `
            <span class="toast-kicker">${onClick ? "New message · Open chat" : "SignalR Demo"}</span>
            <strong>${escapeHtml(title)}</strong>
            <span>${escapeHtml(message)}</span>`;
        if (onClick) toast.addEventListener("click", onClick);
        toastRegion.append(toast);
        window.setTimeout(() => toast.remove(), onClick ? 8000 : 5000);
    }

    window.addEventListener("popstate", () => {
        if (!session) renderLogin();
        else void renderRoute();
    });

    if (session) {
        void enterApplication();
    } else {
        renderLogin();
    }
})();