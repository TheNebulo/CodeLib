const fs = require('fs');
const path = require('path');
const { app } = require('electron');
const axios = require('axios');

exports.getAppDataPath = () => { return app.getPath('userData') }

exports.saveJson = function (pathToSave, inAppData, contents) {
    if (inAppData) pathToSave = path.join(exports.getAppDataPath(), pathToSave);
    try { fs.writeFileSync(pathToSave, JSON.stringify(contents), 'utf8'); }
    catch (err) { console.error(`An error occurred while saving contents to ${pathToSave}`, err); }
    return null;
}

exports.readJson = function (pathToRead, inAppData) {
    if (inAppData) pathToRead = path.join(exports.getAppDataPath(), pathToRead);
    try { if (fs.existsSync(pathToRead)) { return JSON.parse(fs.readFileSync(pathToRead, 'utf8')); } } 
    catch (err) { console.error(`An error occurred while reading ${pathToRead}`, err); }
    return null;
}

exports.getAllJsons = function (jsonsPath, inAppData) {
    if (inAppData) jsonsPath = path.join(exports.getAppDataPath(), jsonsPath);

    try {
        if (!fs.existsSync(jsonsPath)) return null;

        const files = fs.readdirSync(jsonsPath);
        let foundJsons = [];

        for (const file of files) {
            if (path.extname(file) === '.json') {
                const filePath = path.join(jsonsPath, file);
                foundJsons.push([path.basename(filePath) ,filePath]);
            }
        }

        return foundJsons;
    }
    catch (err) {
        console.error(`An error occured while getting all JSONs at ${jsonsPath}`, err);
        return null;
    }
}

exports.createJson = function (pathToCreate, inAppData) {
    if (inAppData) pathToCreate = path.join(exports.getAppDataPath(), pathToCreate);
    try { 
        if (fs.existsSync(pathToCreate)) return;
        exports.saveJson(pathToCreate, false, null);
    } 
    catch (err) { console.error(`An error occurred while creating JSON at ${pathToCreate}`, err); }
    return null;
}

exports.makePath = function (pathToMake, inAppData) {
    if (inAppData) pathToMake = path.join(exports.getAppDataPath(), pathToMake);
    if (!fs.existsSync(pathToMake)) { fs.mkdirSync(pathToMake, { recursive: true }); }
}

exports.removePath = function (pathToRemove, inAppData) {
    if (inAppData) pathToRemove = path.join(exports.getAppDataPath(), pathToRemove);
    if (fs.existsSync(pathToRemove)) { fs.rmSync(pathToRemove, { recursive: true }); }
}

exports.getPathInAppDir = function(pathInAppDir) {
    return path.join(app.getAppPath(), pathInAppDir);
}

exports.Downloader = class Downloader {
    constructor(downloadUrl, outputPath, payload, progressCallback) {
        this.downloadUrl = downloadUrl;
        this.outputPath = outputPath;
        this.payload = payload;
        this.progressCallback = progressCallback;
        this.cancelling = false;
        this.finished = false;

        this.startDownload();
    }

    async startDownload() {
        try {
            const response = await axios({
                method: "post",
                url: this.downloadUrl,
                responseType: "stream",
                data: this.payload,
            });

            const totalBytes = parseInt(response.headers["content-length"], 10);
            let downloadedBytes = 0;

            this.fileStream = fs.createWriteStream(this.outputPath);
            response.data.pipe(this.fileStream);

            response.data.on("data", (chunk) => {
                if (this.cancelling) {
                    this.fileStream.close(() => this._reportStatus("cancelled"));
                    return;
                }

                downloadedBytes += chunk.length;
                this._reportProgress(downloadedBytes, totalBytes);
            });

            this.fileStream.on("finish", () => {
                if (!this.cancelling) this._reportStatus("success");
                this._cleanUp();
                this.finished = true;
            });

            this.fileStream.on("error", (err) => this._reportStatus("error", err.message));

        } catch (error) {
            this._reportStatus("error", error.message);
        }
    }

    cancel() {
        if (!this.fileStream) return;
        this.cancelling = true;
        this.fileStream.close(() => this._cleanUp());
    }

    get isFinished() {
        return this.finished;
    }

    _reportProgress(downloadedBytes, totalBytes) {
        if (this.progressCallback) {
            this.progressCallback({
                status: "progress",
                downloadedBytes: downloadedBytes,
                totalBytes: totalBytes,
                progressPercent: ((downloadedBytes / totalBytes) * 100).toFixed(0),
            });
        }
    }

    _reportStatus(status, error = null) {
        if (this.progressCallback) {
            this.progressCallback({ status: status, error: error });
        }
    }

    _cleanUp() {
        if (this.fileStream) this.fileStream = null;
        this.cancelling = false;
    }
}

