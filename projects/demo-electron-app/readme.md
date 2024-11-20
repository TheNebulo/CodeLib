# Demo Electron App

This a demo `Electron.js` application, that uses a pre-made structure and module for windowManagement.

It can modified for any use cases, and only uses the `electron` and `electron-builder` dev dependicies, to facilitate easy project building.

## Structure

- `app.js` (Starting point)
- `package.json`
- `package-lock.json`
- `build`
    - `icon.png` (Used to create icon for application)
- `pages`
    - A list of page `.html` files and optional `.js` for functionality of UI.
- `resources`
    - All static resources remain here, such as `logo.ico` which is used for notifications.
- `src` (Used for modules)
    - `backend` (Contains backend modules reliant on `ipcMain`)
    - `frontend` (Contain frontend modules relain on `ipcRenderer`)
    - Any other modules that aren't reliant on runtimes

> Backend modules (reliant on `ipcMain`) can only be imported once and in `app.js`.

## Getting Started

Run `npm install` to setup the node modules (requires `Node.js`)

Then start the project using `npm start`.

Package the project using `npm dist` (or `npm pack` to access the binaries)