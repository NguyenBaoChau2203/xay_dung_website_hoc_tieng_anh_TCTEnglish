(function () {
    if (!window.signalR || window.userActivityHubConnection) {
        return;
    }

    const connection = new signalR.HubConnectionBuilder()
        .withUrl("/classChatHub")
        .withAutomaticReconnect()
        .build();

    const readyPromise = new Promise((resolve) => {
        const startConnection = async () => {
            try {
                await connection.start();
                resolve(connection);
            } catch (error) {
                console.error("Unable to start user activity hub connection.", error);
                window.setTimeout(startConnection, 5000);
            }
        };

        startConnection();
    });

    window.userActivityHubConnection = connection;
    window.userActivityHubReady = readyPromise;
})();
