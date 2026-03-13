(function () {
    if (!window.signalR || window.userActivityHubConnection) {
        return;
    }

    const reconnectDelays = [0, 2000, 5000, 10000, 30000];
    const connection = new signalR.HubConnectionBuilder()
        .withUrl("/classChatHub")
        .withAutomaticReconnect(reconnectDelays)
        .build();

    let startPromise = null;
    let isPageUnloading = false;

    function delay(milliseconds) {
        return new Promise((resolve) => window.setTimeout(resolve, milliseconds));
    }

    function dispatchConnectionEvent(name) {
        window.dispatchEvent(new CustomEvent(name, {
            detail: { connection }
        }));
    }

    async function startConnection() {
        if (connection.state === signalR.HubConnectionState.Connected) {
            return connection;
        }

        if (startPromise) {
            return startPromise;
        }

        startPromise = (async () => {
            while (!isPageUnloading) {
                if (connection.state === signalR.HubConnectionState.Connected) {
                    return connection;
                }

                if (connection.state !== signalR.HubConnectionState.Disconnected) {
                    await delay(1000);
                    continue;
                }

                try {
                    await connection.start();
                    dispatchConnectionEvent("user-activity:connected");
                    return connection;
                } catch (error) {
                    console.error("Unable to start user activity hub connection.", error);
                    await delay(5000);
                }
            }

            return connection;
        })();

        try {
            return await startPromise;
        } finally {
            startPromise = null;
        }
    }

    connection.onreconnecting(() => {
        dispatchConnectionEvent("user-activity:reconnecting");
    });

    connection.onreconnected(() => {
        dispatchConnectionEvent("user-activity:reconnected");
    });

    connection.onclose(() => {
        dispatchConnectionEvent("user-activity:closed");
        if (!isPageUnloading) {
            void startConnection();
        }
    });

    window.addEventListener("beforeunload", () => {
        isPageUnloading = true;
    });

    window.userActivityHubConnection = connection;
    window.userActivityHubReady = startConnection();
})();
