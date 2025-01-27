/*
REQUIRED APP.JS SETUP:

const windowManager = require("./src/backend/windowManager");

windowManager.enableChromiumTools = false; // Used to provide access to chromium dev tools

windowManager.onWindowPresetOpened = function (fileName) {
    return { domCallbacks : {} } // More options are available such asyncTasks
}
    
windowManager.onPopoutWindowPresetOpened = function (fileName) { 
    return { domCallbacks : { } } // More options are available such asyncTasks
};

windowManager.addWindowPreset('home', 900, 600);  // Change as needed, just an example

// On app ready make sure to open a window like so:
// windowManager.openWindowPreset('home');

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

*/

const { BrowserWindow, Menu, Notification, ipcMain, shell } = require('electron');
const fs = require('fs');
const path = require('path');

let mainWindow;
let isMainWindowClosing = true;
let popoutWindows = {};
let arePopoutWindowsClosing = false;
let currentWindowPreset;

exports.getMainWindow = () => { return mainWindow; }

exports.getPopoutWindow = (presetName) => {
    if (presetName in popoutWindows) return popoutWindows[presetName];
    return null;
}

exports.getAllPopoutWindows = () => { return popoutWindows; }

exports.enableChromiumTools = false;
exports.onWindowPresetOpened = null;
exports.onPopoutWindowPresetOpened = null;

let windowPresets = {};
let popoutWindowPresets = {};

exports.addWindowPreset = function (presetName, width, height, defaultCallback) {
    if (windowPresets[presetName]) return;

    windowPresets[presetName] = {
        width: width,
        height: height,
        defaultCallback: defaultCallback
    };
}

exports.addPopoutWindowPreset = function (presetName, width, height, persistent=false, defaultCallback=null) {
    if (popoutWindowPresets[presetName]) return;

    popoutWindowPresets[presetName] = {
        width: width,
        height: height,
        persistent: persistent,
        defaultCallback: defaultCallback
    };
}

exports.openWindowPreset = function (presetName, additionalCallback) {
    if (!windowPresets[presetName]) return;
    let wp = windowPresets[presetName];
    currentWindowPreset = presetName;

    createWindow(presetName, wp.width, wp.height, () => {
        let { domCallbacks, asyncTask } = exports.onWindowPresetOpened(presetName);
        
        if (domCallbacks) { 
            for (const [channel, value] of Object.entries(domCallbacks)) {
                exports.sendMessage(channel, value);
            }
        }

        if (asyncTask) {
            asyncTask().then(shouldContinue => {
                if (shouldContinue === false) return;
                if (wp.defaultCallback) wp.defaultCallback();
                if (additionalCallback) additionalCallback();
            })    
        }
        else {
            if (wp.defaultCallback) wp.defaultCallback();
            if (additionalCallback) additionalCallback();
        }
    });
}

exports.openPopoutWindowPreset = function (presetName, additionalCallback) {
    if (!popoutWindowPresets[presetName]) return;
    let wp = popoutWindowPresets[presetName];

    createPopoutWindow(presetName, wp.width, wp.height, wp.persistent, () => {
        let { domCallbacks, asyncTask } = exports.onPopoutWindowPresetOpened(presetName);

        if (domCallbacks) { 
            for (const [channel, value] of Object.entries(domCallbacks)) {
                exports.sendPopoutMessage(presetName, channel, value);
            }
        }

        if (asyncTask) {
            asyncTask().then(shouldContinue => {
                if (shouldContinue === false) return;
                if (wp.defaultCallback) wp.defaultCallback();
                if (additionalCallback) additionalCallback();
            })    
        }
        else {
            if (wp.defaultCallback) wp.defaultCallback();
            if (additionalCallback) additionalCallback();
        }
    });
}

function createWindow(presetName, width, height, callback) {
    if (mainWindow) { 
        isMainWindowClosing = false;
        exports.closeMainWindow(); 
    }

    mainWindow = new BrowserWindow({
        width: width,
        height: height,
        icon: path.join(__dirname, '../../resources/logo.ico'),
        webPreferences: {
            preload: path.join(__dirname, `../../pages/${presetName}.js`),
            nodeIntegration: false,
            contextIsolation: true,
            enableRemoteModule: false,
            sandbox: false
        }
    });
    
    mainWindow.loadFile(`./pages/${presetName}.html`);
    mainWindow.setResizable(exports.enableChromiumTools);
    if (!exports.enableChromiumTools) { Menu.setApplicationMenu(null); }

    mainWindow.on('closed', () => {
        if (isMainWindowClosing) exports.closeAllPopoutWindows();  // Close all popout windows if closing
        isMainWindowClosing = true;
    });

    mainWindow.webContents.once('did-finish-load', () => {
        mainWindow.show(); 
        if (callback) callback();
    });
}

function createPopoutWindow(presetName, width, height, persistent, callback) {
    if (popoutWindows[presetName]) { exports.closePopoutWindow(presetName); }

    popoutWindows[presetName] = new BrowserWindow({
        width: width,
        height: height,
        icon: path.join(__dirname, '../../resources/logo.ico'),
        webPreferences: {
            preload: path.join(__dirname, `../../pages/${presetName}.js`),
            nodeIntegration: false,
            contextIsolation: true,
            enableRemoteModule: false,
            sandbox: false
        }
    });
    
    popoutWindows[presetName].loadFile(`./pages/${presetName}.html`);
    popoutWindows[presetName].setResizable(exports.enableChromiumTools);
    if (!exports.enableChromiumTools) { Menu.setApplicationMenu(null); }

    popoutWindows[presetName].on('closed', () => {
        if (arePopoutWindowsClosing) { arePopoutWindowsClosing = false; return; }
        delete popoutWindows[presetName];
        if (persistent) createPopoutWindow(presetName, width, height, persistent, callback);
    });

    popoutWindows[presetName].webContents.once('did-finish-load', () => {
        popoutWindows[presetName].show(); 
        if (callback) callback();
    });
}

exports.reloadCurrentWindow = (callback) => {
    exports.openWindowPreset(currentWindowPreset, () => {
        if (callback) callback();
    });
}

exports.reloadPopoutWindow = (presetName, callback) => {
    exports.openPopoutWindowPreset(presetName, () => {
        if (callback) callback();
    });
}

exports.reloadAllPopoutWindows = () => {
    Object.keys(popoutWindows).forEach(presetName => {
        exports.reloadPopoutWindow(presetName);
    })
} 

exports.sendMessage = (channel, ...args) => {
    if (mainWindow && mainWindow.webContents) {
        mainWindow.webContents.send(channel, ...args);
    }
};

exports.sendPopoutMessage = (presetName, channel, ...args) => {
    if (popoutWindows[presetName] && popoutWindows[presetName].webContents) {
        popoutWindows[presetName].webContents.send(channel, ...args);
    }
}

exports.sendNotification = function (title, description, length) {
    exports.sendMessage('notification', title, description, length);
}

exports.showMainWindow = () => { mainWindow.show(); }

exports.closeMainWindow = function () {
    if (mainWindow) {
        try { mainWindow.close(); }
        catch (err) { } // It's fine, its probably destroyed anyway
    }
    mainWindow = null;
}

exports.showPopoutWindow = (presetName) => { if (popoutWindows[presetName]) popoutWindows[presetName].show(); }

exports.closePopoutWindow = (presetName) => {
    arePopoutWindowsClosing = true;

    if (popoutWindows[presetName]) {
        try { popoutWindows[presetName].close(); }
        catch (err) { } // It's fine, its probably destroyed anyway

        delete popoutWindows[presetName];
    }
}

exports.closeAllPopoutWindows = () => { Object.keys(popoutWindows).forEach(presetName => { exports.closePopoutWindow(presetName); }) }

ipcMain.on('reload', (event, additionalCallback) => {
    exports.reloadCurrentWindow(additionalCallback);
});

ipcMain.on('open-window-preset', (event, presetName, additionalCallback) => {
    exports.openWindowPreset(presetName, additionalCallback);
});

ipcMain.on('open-popout-window-preset', (event, presetName, additionalCallback) => {
    exports.openPopoutWindowPreset(presetName, additionalCallback);
});

ipcMain.on('close-popout-window', (event, presetName) => {
    exports.closePopoutWindow(presetName);
});

ipcMain.on('reflect', (event, channel, ...args) => { exports.sendMessage(channel, ...args); });

ipcMain.on('notify-os', (event, title, description) => { new Notification({ title: title, body: description, icon: 'resources/logo.ico' }).show(); })

ipcMain.on('open-file', (event, filePath) => {
    if (!fs.existsSync(filePath)) return;
    shell.showItemInFolder(filePath)
});

ipcMain.on('open-external-url', (event, url) => {
    shell.openExternal(url);
});