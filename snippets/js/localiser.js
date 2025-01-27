// DOM nodes that need localising can place locale tags in the following syntax: [!:tag{var1}{var2}]
// The localiser module will automatically localise the DOM upon all changes

let localisationStrings = JSON.parse(atob(document.querySelector('#locale-b64').content)); // Change as you will
let observer = null;

function getLocalString(tag, variables = []) {
    tag = String(tag);

    if (!(tag in localisationStrings)) {
        console.error(`Tried to localise tag ${tag}, which doesn't exist.`);
        const variablesString = variables.map((v) => `{${v}}`).join('');
        return `[!:${tag}${variablesString}]`;
    }

    let localisedString = localisationStrings[tag];

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
                variables.push(value.trim());
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
            
            const localString = getLocalString(tag, variables);
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
                    
                    const localString = getLocalString(tag, variables);
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

observer = new MutationObserver((mutationsList) => {
    for (const mutation of mutationsList) { localiseNode(mutation.target); }
});

localiseNode(document.body);