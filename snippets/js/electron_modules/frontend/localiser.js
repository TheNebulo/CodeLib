/*
REQUIRED APP.JS SETUP: (RELIANT ON FILE MANAGER)

// Localisation setup
let localisationStrings = {};

fm.getAllJsons(fm.getPathInAppDir("/resources/locales/")).forEach(([fileName, jsonPath]) => {
    localeContent = fm.readJson(jsonPath);
    for (const [key, value] of Object.entries(localeContent)) {
        if (!(key in localisationStrings)) localisationStrings[key] = {};
        localisationStrings[key][fileName.split(".")[0]] = value;
    }
});

ipcMain.handle('get-localisation-strings', () => { return localisationStrings; });

// Upon DOM init, an ipcMain event should be sent on channel 'localise' containing the current language code like so:
ipcMain.send('localise', 'en');

// It is highly reccomended that windows are reloaded upon language change as HTML will not relocalise without reload.
// Locale files in this example are kept like so:

// resources
// -> locales
//    -> en.json
//    -> ru.json
//    -> languageCode.json

// JSON files are structured like so:

// { "tag1" : "localTranslation1", "tag2" : "localTranslation2" }

*/



/*
REQUIRED FRONTEND SETUP AND USAGE NOTES:
const localiser = require("../src/frontend/localiser");

// HTML files that want localising can place locale tags in the following syntax: [!:tag]
// The localiser module will automatically localise the DOM upon init, and init only.

// Any additional localisation during runtime should be handled via javascript like so:
localiser.getLocalString("tag", { variable: value });' // Variables are optional
*/

const { ipcRenderer } = require('electron');

let localisationStrings = {};
let lang = 'en';

ipcRenderer.invoke('get-localisation-strings').then((data) => { localisationStrings = data; });

exports.getLocalString = function (tag, variables = {}) {
    tag = String(tag);

    if (!(tag in localisationStrings)) {
        console.error(`Tried to localise tag ${tag}, which doesn't exist.`);
        return `[!:${tag}]`;
    }

    let localisedString = localisationStrings[tag][lang] || localisationStrings[tag].en;

    // Replace placeholders in the localized string with actual values
    for (const [key, value] of Object.entries(variables)) {
        localisedString = localisedString.replace(new RegExp(`\\{${key}\\}`, 'g'), value);
    }

    return localisedString;
}

exports.localiseHtml = function () {
    const customTagPattern = /\[!:(.*?)\]/g;

    function replaceInTextNode(node) {
        let textNodeValue = node.nodeValue;
        const matches = [...textNodeValue.matchAll(customTagPattern)];

        if (matches.length > 0) {
            matches.forEach(match => {
                const tag = match[1];
                const localString = exports.getLocalString(tag);
                textNodeValue = textNodeValue.replace(match[0], localString);
            });
        }

        node.nodeValue = textNodeValue;
    }
    

    function replaceInAttributes(node) {
        const attributes = node.attributes;
        if (attributes) {
            for (let attr of attributes) {
                let attrValue = attr.value;
                let match;
                customTagPattern.lastIndex = 0;  // Reset regex state for each attribute
                while ((match = customTagPattern.exec(attrValue)) !== null) {
                    const tag = match[1];
                    const localString = exports.getLocalString(tag);
                    attrValue = attrValue.replace(match[0], localString);
                    customTagPattern.lastIndex = 0;
                }
                node.setAttribute(attr.name, attrValue); // Set the replaced value back
            }
        }
    }

    function traverseAndReplace(node) {
        if (node.nodeType === Node.TEXT_NODE) {
            replaceInTextNode(node);
        } else {
            replaceInAttributes(node); // Handle attributes for non-text nodes
            node.childNodes.forEach(child => traverseAndReplace(child)); // Recursively replace in children
        }
    }

    traverseAndReplace(document.body);
}


ipcRenderer.on('localise', (event, newLang) => {
    if (newLang) lang = newLang;
    exports.localiseHtml();
});