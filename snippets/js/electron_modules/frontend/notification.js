/*
REQUIRED FRONTEND SETUP AND USAGE NOTES:

const notification = require("../src/frontend/notification");
notification.injectUi();

// When a notification is needed, call activate();
notification.activate("Title", "Description", 2000);
*/

const { ipcRenderer } = require("electron");

let notificationObject = null;
let activateNotifications = 0;

exports.injectUi = function (parentNode=null, injectStyling=true) {
    // Change styling as you will (this was extracted from another project out of context)
    let defaultStyling = `
        .notification {
            position: absolute;
            top: 0;
            padding-top: 4px;
            padding-bottom: 8px;
            height: 115px;
            width: 100vw;
            background: var(--primary-color);
            background: linear-gradient(90deg, var(--secondary-color) 0%, var(--primary-color) 100%);
            box-shadow: 0px 0px 10px 5px rgba(0, 0, 0, 0.2);
            opacity: 0;
            transition: all ease-in-out 0.25s;
            z-index: 1000;
            pointer-events: none;
        }

        .notification > h1 {
            margin-top: 16.5px;
            margin-left: 20px;
            color: white;
            font-weight: 700;
            font-size: 30px;
        }

        .notification > span {
            margin-left: 20px;
            color: white;
            font-size: 20px;
        }

        .notification.active {
            pointer-events: all;
            opacity: 1;
        }
    `

    if (injectStyling) {
        let styleSheet = document.createElement("style");
        styleSheet.textContent = defaultStyling;
        document.head.appendChild(styleSheet);
    }

    notificationObject = document.createElement("div");
    notificationObject.classList.add("notification");
    notificationObject.innerHTML = "<h1></h1><span></span>";

    if (parentNode) parentNode.appendChild(notificationObject);
    else document.body.appendChild(notificationObject);
}

exports.activate = function (title, description, length, notifyOs=false, onEndCallback=null) {
    if (!notificationObject) exports.injectUi();

    notificationObject.querySelector('h1').textContent = title;
    notificationObject.querySelector('span').textContent = description;
    notificationObject.classList.add('active');
    activateNotifications++;

    setTimeout(() => {
        activateNotifications--;
        
        let stillExists = document.contains(notificationObject) != null;
        if (!stillExists) return; // Assume that the page has changed and JS callbacks are no longer valid.

        if (activateNotifications == 0) { notificationObject.classList.remove('active'); }

        if (onEndCallback) onEndCallback();

    }, length);

    if (notifyOs) ipcRenderer.send('notify-os', title, description);
}

ipcRenderer.on('notification', (event, title, description, length) => {
    exports.activate(title, description, length);
    ipcRenderer.send("reflect", "localise");
});