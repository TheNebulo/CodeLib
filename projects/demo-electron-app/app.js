const { app, globalShortcut } = require('electron');

const windowManager = require("./src/backend/windowManager");

// Dev/testing tools (MUST BE SET TO FALSE BEFORE LEAVING DEV ENVIRONMENT)
windowManager.enableChromiumTools = false; // Used to provide access to chromium dev tools
let forceOpenCallbackConsole = false; // Used to force open a developer IPC callback console

windowManager.addWindowPreset('demo', 900, 600);
windowManager.addPopoutWindowPreset('console', 600, 900, forceOpenCallbackConsole);

windowManager.onWindowPresetOpened = function (fileName) {
    return {
        domCallbacks : {},

        // This section should be uncommented if users should be manually authenticated on page loads. Currently unused as all server requests handle this logic behind the scenes.
        /* asyncTask : async () => {
            if (fileName == "login" || fileName == "update") return;
            
            let {ok, status} = await auth.authenticateUser();
            if (!ok) return onFailedRequest(status);
            return true;
        } */
    }
}

windowManager.onPopoutWindowPresetOpened = function (fileName) { 
    return { domCallbacks : { } }
};

app.on("ready", () => {
    if (!app.requestSingleInstanceLock()) { 
        console.error("Failed to obtain instance lock. Quitting...")
        app.quit();
        return;
    }

    if (process.platform == 'win32') { app.setAppUserModelId("com.thenebulo.demoelectronapp"); }
    windowManager.openWindowPreset('demo');

    if (forceOpenCallbackConsole) { windowManager.openPopoutWindowPreset('console'); return; }

    globalShortcut.register('Alt+`', () => {
        if (windowManager.getMainWindow().isFocused()) { windowManager.openPopoutWindowPreset('console'); return; }

        Object.values(windowManager.getAllPopoutWindows()).forEach(popoutWindow => {
            if (popoutWindow.isFocused()) { windowManager.openPopoutWindowPreset('console'); return; }
        });
    });
});

app.on('second-instance', (event, commandLine, workingDirectory) => {
    if (windowManager.getMainWindow()) {
        if (windowManager.getMainWindow().isMinimized()) { windowManager.getMainWindow().restore(); }
        windowManager.getMainWindow().focus();
    }
});

app.on('window-all-closed', () => {
    windowManager.closeAllPopoutWindows();
    windowManager.closeMainWindow();
    if (process.platform !== 'darwin') { app.quit(); }
});