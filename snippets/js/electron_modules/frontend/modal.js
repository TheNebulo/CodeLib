/*
REQUIRED FRONTEND SETUP AND USAGE NOTES:

const modal = require("../src/frontend/modal");
modal.injectUi();

// When a modal is needed, call activate();
modal.activate(
    "Title",
    "Description",
    "Button Text",
    false, // Whether modal does a critical/important action
    () => {
        // Handle successCallback
    }
);
*/

let modalObject = null;

exports.injectUi = () => {
    // Change styling as you will (this was extracted from another project out of context)
    let defaultStyling =  `
    .modal {
        position: fixed;
        width: 100%;
        height: 100%;
        top: 0;
        background-color: rgba(0,0,0,0.7);
        z-index: 10000;
        display: flex;
        align-items: center;
        justify-content: center;
        pointer-events: none;
        opacity: 0;
        transition: all ease-in-out 0.2s;
    }

    .modal.active {
        opacity: 1;
        pointer-events: all;
    }

    .modal > .container {
        background: var(--primary-color);
        background: linear-gradient(90deg, var(--secondary-color) 0%, var(--primary-color) 100%);
        box-shadow: 0px 0px 10px 5px rgba(0, 0, 0, 0.2);
        width: 26%;
        height: fit-content;
        color: white;
        padding: 2%;
        border-radius: 10px;
    }

    .modal > .container > h2 {
        font-weight: 700;
    }

    .modal > .container > p {
        margin-top: 10px;
        font-size: 14px;
    }

    .modal > .container > .buttons {
        margin-top: 15px;
        display: flex;
        flex-direction: row;
        width: 100%;
    }

    .modal > .container > .buttons > .button {
        padding: 10px;
        width: fit-content;
        background-color: black;
        color: white;
        border-radius: 10px;
        margin-right: 10px;
        font-size: 14px;
        cursor: pointer;
        font-weight: 600;
    }

    .modal > .container > .buttons > .button.add {
        background-color: green;
        color: white;
    }

    .modal > .container > .buttons > .button.remove {
        background-color: red;
        color: white;
    }

    .modal > .container > select {
        padding: 10px;
        width: 100%;
        background-color: white;
        color: black;
        margin-top: 10px;
        border-radius: 10px;
        font-size: 14px;
        cursor: pointer;
        font-weight: 600;
        border: none;
        outline: none;
        appearance: none;
        background-image: url("./../resources/dropdown-arrow.svg");
        background-repeat: no-repeat;
        background-position: right 0.7rem top 50%;
        background-size: 0.65rem auto;
    }

    .modal > .container > select.disabled {
        display: none;
    }

    .modal > .container > select > option {
        font-weight: 600;
    }

    .modal > .container > input {
        padding: 10px;
        width: 100%;
        background-color: white;
        color: black;
        margin-top: 10px;
        border-radius: 10px;
        font-size: 14px;
        font-weight: 600;
        border: none;
        outline: none;
        appearance: none;
    }

    .modal > .container > input.disabled {
        display: none;
    }
    `;

    let styleSheet = document.createElement("style");
    styleSheet.textContent = defaultStyling;
    document.head.appendChild(styleSheet);

    modalObject = document.createElement("div");
    modalObject.classList.add("modal");
    modalObject.innerHTML = `
    <div class="container">
        <h2></h2>
        <p></p>
        <select class="disabled"></select>
        <input class="disabled"></select>
        <div class="buttons">
            <div class="primary button"></div>
            <div class="cancel button">[!:cancel]</div>
        </div>
    </div>
    `;

    document.body.appendChild(modalObject);
}

exports.activate = function (title, description, buttonText, isCritical, successCallback, additionalInputOptions) {
    if (!modalObject) exports.injectUi();

    let titleEl = modalObject.querySelector("h2");
    let descriptionEl = modalObject.querySelector("p");
    let primaryButton = modalObject.querySelector(".primary.button");
    let cancelButton = modalObject.querySelector(".cancel.button");
    let dropdown = modalObject.querySelector("select");
    let input = modalObject.querySelector("input");

    primaryButton.classList.remove(...primaryButton.classList);
    primaryButton.textContent = buttonText;
    primaryButton.classList.add('button');
    primaryButton.classList.add('primary');
    primaryButton.classList.add(isCritical ? "remove" : "add");

    primaryButton.addEventListener('click', (event) => {
        modalObject.classList.remove('active');

        let response = {
            dropdownValue: null,
            inputValue: null,
        };

        if (additionalInputOptions && additionalInputOptions.dropdownOptions) response.dropdownValue = dropdown.value;
        if (additionalInputOptions && additionalInputOptions.inputPlaceholder) response.inputValue = input.value;

        successCallback(response);
    });

    cancelButton.addEventListener('click', (event) => {
        modalObject.classList.remove('active');
    });

    titleEl.textContent = title;
    descriptionEl.textContent = description;

    if (additionalInputOptions && additionalInputOptions.dropdownOptions) {
        dropdown.innerHTML = '';
        dropdown.classList.remove('disabled');

        additionalInputOptions.dropdownOptions.forEach(option => {
            let optionEl = document.createElement('option');
            optionEl.value = option.value;
            optionEl.innerText = option.alias;
            optionEl.selected = option.selected;

            dropdown.appendChild(optionEl);
        });
    }
    else {
        dropdown.classList.add('disabled');
    }
    
    if (additionalInputOptions && additionalInputOptions.inputPlaceholder) {
        input.placeholder = additionalInputOptions.inputPlaceholder;
        input.classList.remove('disabled');
    }
    else {
        input.classList.add('disabled');
    }

    modalObject.classList.add('active');
}