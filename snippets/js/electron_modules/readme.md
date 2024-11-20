# Snippets of Electron.js Modules

Snippets here are used for modules loaded in `Electron.js`, which operate in a `Node.js` environment.

If a module is in this root folder, it isn't forced to be in the backend (`ipcMain`) or (`ipcRenderer`) runtime.

Instructions for implementation are present in the header of each file, if necessary.

Otherwise, it usually is easy to implement anywhere in the code.

Modules shouldn't import one another unless they aren't attached to a runtime like `fileManager.js`.

> Each module will require it's modules that can be found at the top of the file.