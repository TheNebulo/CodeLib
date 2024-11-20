/*
REQUIRED APP.JS SETUP: (RELIANT ON FILE MANAGER)

const auth = require("./src/backend/auth");

auth.bypassAuth = false; // Used to skip auth to access UI offline

auth.onLogout = (silent) => {
    // Handle procedures for onLogout event

    if (silent) return;

    // Handle notification effects for logout
};
*/


const { ipcMain } = require('electron');
const axios = require('axios');
const fm = require('../fileManager');

let currentUser = {
    playtesterId: null,
    serverUrl: null
}

exports.onLogout = null;
exports.bypassAuth = false;

exports.getUser = () => { return currentUser; }

exports.isUserValid = () => { return (currentUser.playtesterId !== null && currentUser.serverUrl !== null); }

exports.loadUser = function () {
    userJson = fm.readJson('credentials.json', true);

    if (!userJson) {
        currentUser.playtesterId = null;
        currentUser.serverUrl = null;
        return;
    }
    
    currentUser.playtesterId = userJson.playtesterId;
    currentUser.serverUrl = userJson.serverUrl;
}

exports.saveUser = function () {
    fm.saveJson("credentials.json", true, currentUser);
}

exports.setUser = function (playtesterId, serverUrl) {
    currentUser.playtesterId = playtesterId;
    currentUser.serverUrl = serverUrl;
}

exports.authenticateUser = async function () {
    if (exports.bypassAuth) return { ok: true, status: 200 };
    if (!exports.isUserValid) { return { ok: false, status: -1 }; }

    try {
        // Change axios request methods and values as needed
        const resp = await axios.post(`${currentUser.serverUrl}/api/validate-access`, { playtester_id: currentUser.playtesterId });
        return { ok: resp.status === 200, status: resp.status };
    } 
    catch (error) {
        let status = 0; // Default status for no internet connection
        if (error.response) { status = error.response.status; }

        return { ok: false, status: status };
    }
}

exports.login = async function () {
    let {ok, status} = await exports.authenticateUser();

    if (ok) {
        exports.saveUser();
        return { success: true };
    }

    return { success: false, status: status };
}

exports.logout = function (silent=false) {
    exports.setUser(null, null);
    fm.removePath("credentials.json", true);
    if (exports.onLogout) exports.onLogout(silent);
}

ipcMain.handle('login', async (event, { playtesterId, serverUrl }) => {
    exports.setUser(playtesterId, serverUrl);
    let response = await exports.login();
    return response;
});

ipcMain.on('logout', (event) => {
    exports.logout(false);
});

ipcMain.handle('get-user', (event) => { return exports.getUser(); })