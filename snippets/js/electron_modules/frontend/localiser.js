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
require("../src/frontend/localiser");

// DOM nodes that need localising can place locale tags in the following syntax: [!:tag{var1}{var2}]
// The localiser module will automatically localise the DOM upon all changes
*/

const { ipcRenderer } = require('electron');

let localisationStrings = {};
let lang = 'en';
let observer = null;

ipcRenderer.invoke('get-localisation-strings').then((data) => { localisationStrings = data; });

exports.getLocalString = function (tag, variables = []) {
    tag = String(tag);

    if (!(tag in localisationStrings)) {
        console.error(`Tried to localise tag ${tag}, which doesn't exist.`);
        const variablesString = variables.map((v) => `{${v}}`).join('');
        return `[!:${tag}${variablesString}]`;
    }

    let localisedString = localisationStrings[tag][lang] || localisationStrings[tag].en;

    variables.forEach((value, index) => {
        localisedString = localisedString.replace(new RegExp(`\\{${index + 1}\\}`, 'g'), value);
    });

    return localisedString;
};

function localiseNode(node) {
    observer.disconnect();

    const customTagPattern = /!\:(\w+)((?:\{.*?\})*)/g;
    const localisationTagPattern = /\[!\:(\w+)((?:\{.*?\})*)\]/g;

    function findLocalisationTags(str) {
        const matches = [];
        let match;
    
        while ((match = localisationTagPattern.exec(str)) !== null) {
            matches.push(match[0]);
        }
    
        return matches;
    }

    function parseTagAndVariables(str) {
        const match = customTagPattern.exec(str);
        if (!match) return null; 
    
        const tag = match[1];
        const rawVariables = match[2];

        const variables = [];
        if (rawVariables) {
            rawVariables.match(/\{(.*?)\}/g)?.forEach((varString) => {
                const value = varString.slice(1, -1);
                variables.push(value);
            });
        }

        return { tag, variables };
    }

    function replaceInTextNode(node) {
        if (!node.nodeValue) return;

        let matches = findLocalisationTags(node.nodeValue);

        matches.forEach((match) => {
            const data = parseTagAndVariables(match);
            if (!data) return;

            let { tag, variables } = data;
            
            const localString = exports.getLocalString(tag, variables);
            node.nodeValue = node.nodeValue.replace(match, localString);
        });
    }

    function replaceInAttributes(node) {
        const attributes = node.attributes;
        if (attributes) {
            for (let attr of attributes) {
                if (!attr.value) continue;

                let matches = findLocalisationTags(attr.value);

                matches.forEach((match) => {
                    const data = parseTagAndVariables(match);
                    if (!data) return;

                    let { tag, variables } = data;
                    
                    const localString = exports.getLocalString(tag, variables);
                    node.setAttribute(attr.name, node.getAttribute(attr.name).replace(match, localString));
                });
            }
        }
    }

    if (node.nodeType === Node.TEXT_NODE) {
        replaceInTextNode(node);
    } else if (node.nodeType === Node.ELEMENT_NODE) {
        replaceInAttributes(node);
        node.childNodes.forEach(child => localiseNode(child));
    }

    observer.observe(document.body, {
        childList: true,
        attributes: true,
        subtree: true,
        characterData: true
    });
}

exports.localiseHtml = function () {
    localiseNode(document.body);
};

document.addEventListener('DOMContentLoaded', () => {
    observer = new MutationObserver((mutationsList) => {
        for (const mutation of mutationsList) { localiseNode(mutation.target); }
    });

    ipcRenderer.on('localise', (event, newLang) => {
        if (newLang) lang = newLang;
        exports.localiseHtml();
    });
});