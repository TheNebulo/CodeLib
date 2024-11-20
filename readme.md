# CodeLib

**CodeLib** is a collection of code snippets and projects that I use commonly throughout projects. These snippets are not attached to a specfific context, and when they are, the context is clearly defined.

Projects and snippets can be found in their respective folders, with their own respective `readme.md` files.

Feel free to copy and paste any files here into your own projects freely, no licensing strings are attached.

> Any suggestions for snippets and projects (including fixes) are always welcome!

## Available Snippets

### Python (Vanilla)

- `elo_system.py` - An elo calculator between a winning and losing party (WIP).
- `json_config.py` - A config loader using a specific JSON config convention of mine.
- `phone_number_formatting.py` - Name speaks for itself, checks formatting of phone number strings.
- `regex.py` - A collection of useful regex check functions.

### Python (Flask)

- `endpoint_decorators.py` - A list of decorators for Flask endpoints for quickly handling repetitive logic such as database connection and authentication.
- `database.py` - A basic and barebones psycopg2 wrapper to easily integrate database work in Flask.
- `url_utils.py` - A list of functions to handle URL sanitizing and URl checks for safety.

### JavaScript (Vanilla)

- `aspectRatio.py` - A weird implementation of applying different CSS files based on screen aspect ratios to make better use of viewport units.

### JavaScript (Electron.js Pages)

- `console.html/js` - Extensive developer console that allows for quick sending of ipcMain and ipcRenderer events (with arguments) in an external environment for testing.

### JavaScript (Electron.js Modules)

- `auth.js` - A backend module that handles authentication with an external provider. Actual authentication logic needs to be changed per use-case, but provides a very nice set of utilities to keep authentication checks easy.

- `updateManager.js` - A backend module that makes use of the electron-updater module that allows for simple update checks and management in your electron project.

- `windowManager.js` - An extensive backend module that makes window creation and management in electron super easy using pre-defined window presets and a ton of customisable callback events, making window behaviour predictable and controlled.

- `localiser.js` - A frontend module (with required backend integration) that adds a localisation system to your electron projects, that is flexible and extensive, on both the HTML and JavaScript front,

- `modal.js` - A frontend module for injecting modal UI and managing it's activation and output through simple one-line callback events to avoid redundancy. Actual modal UI needs to be update per-project.

- `notification.js` - A frontend module for injecting notification UI and easily calling notification on the page through simple one-line callback events to avoid redundancy. Actual modal UI needs to be update per-project.

- `fileManager.js` - A runtime-less module that can be imported anywhere throughout the project to streamline usecases of the fs module, such as reading AppData and reading/writing JSON files.

## Available Projects

- `demo-electron-app`
    - A barebones application for Electron following my own structuring conventions.