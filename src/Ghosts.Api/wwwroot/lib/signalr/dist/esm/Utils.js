// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
var __assign = (this && this.__assign) || Object.assign || function(t) {
    for (var s, i = 1, n = arguments.length; i < n; i++) {
        s = arguments[i];
        for (var p in s) if (Object.prototype.hasOwnProperty.call(s, p))
            t[p] = s[p];
    }
    return t;
};
var __awaiter = (this && this.__awaiter) || function (thisArg, _arguments, P, generator) {
    return new (P || (P = Promise))(function (resolve, reject) {
        function fulfilled(value) { try { step(generator.next(value)); } catch (e) { reject(e); } }
        function rejected(value) { try { step(generator["throw"](value)); } catch (e) { reject(e); } }
        function step(result) { result.done ? resolve(result.value) : new P(function (resolve) { resolve(result.value); }).then(fulfilled, rejected); }
        step((generator = generator.apply(thisArg, _arguments || [])).next());
    });
};
var __generator = (this && this.__generator) || function (thisArg, body) {
    var _ = { label: 0, sent: function() { if (t[0] & 1) throw t[1]; return t[1]; }, trys: [], ops: [] }, f, y, t, g;
    return g = { next: verb(0), "throw": verb(1), "return": verb(2) }, typeof Symbol === "function" && (g[Symbol.iterator] = function() { return this; }), g;
    function verb(n) { return function (v) { return step([n, v]); }; }
    function step(op) {
        if (f) throw new TypeError("Generator is already executing.");
        while (_) try {
            if (f = 1, y && (t = op[0] & 2 ? y["return"] : op[0] ? y["throw"] || ((t = y["return"]) && t.call(y), 0) : y.next) && !(t = t.call(y, op[1])).done) return t;
            if (y = 0, t) op = [op[0] & 2, t.value];
            switch (op[0]) {
                case 0: case 1: t = op; break;
                case 4: _.label++; return { value: op[1], done: false };
                case 5: _.label++; y = op[1]; op = [0]; continue;
                case 7: op = _.ops.pop(); _.trys.pop(); continue;
                default:
                    if (!(t = _.trys, t = t.length > 0 && t[t.length - 1]) && (op[0] === 6 || op[0] === 2)) { _ = 0; continue; }
                    if (op[0] === 3 && (!t || (op[1] > t[0] && op[1] < t[3]))) { _.label = op[1]; break; }
                    if (op[0] === 6 && _.label < t[1]) { _.label = t[1]; t = op; break; }
                    if (t && _.label < t[2]) { _.label = t[2]; _.ops.push(op); break; }
                    if (t[2]) _.ops.pop();
                    _.trys.pop(); continue;
            }
            op = body.call(thisArg, _);
        } catch (e) { op = [6, e]; y = 0; } finally { f = t = 0; }
        if (op[0] & 5) throw op[1]; return { value: op[0] ? op[1] : void 0, done: true };
    }
};
import { LogLevel } from "./ILogger";
import { NullLogger } from "./Loggers";
// Version token that will be replaced by the prepack command
/** The version of the SignalR client. */
export var VERSION = "5.0.0";
/** @private */
var Arg = /** @class */ (function () {
    function Arg() {
    }
    Arg.isRequired = function (val, name) {
        if (val === null || val === undefined) {
            throw new Error("The '" + name + "' argument is required.");
        }
    };
    Arg.isNotEmpty = function (val, name) {
        if (!val || val.match(/^\s*$/)) {
            throw new Error("The '" + name + "' argument should not be empty.");
        }
    };
    Arg.isIn = function (val, values, name) {
        // TypeScript enums have keys for **both** the name and the value of each enum member on the type itself.
        if (!(val in values)) {
            throw new Error("Unknown " + name + " value: " + val + ".");
        }
    };
    return Arg;
}());
export { Arg };
/** @private */
var Platform = /** @class */ (function () {
    function Platform() {
    }
    Object.defineProperty(Platform, "isBrowser", {
        get: function () {
            return typeof window === "object";
        },
        enumerable: true,
        configurable: true
    });
    Object.defineProperty(Platform, "isWebWorker", {
        get: function () {
            return typeof self === "object" && "importScripts" in self;
        },
        enumerable: true,
        configurable: true
    });
    Object.defineProperty(Platform, "isNode", {
        get: function () {
            return !this.isBrowser && !this.isWebWorker;
        },
        enumerable: true,
        configurable: true
    });
    return Platform;
}());
export { Platform };
/** @private */
export function getDataDetail(data, includeContent) {
    var detail = "";
    if (isArrayBuffer(data)) {
        detail = "Binary data of length " + data.byteLength;
        if (includeContent) {
            detail += ". Content: '" + formatArrayBuffer(data) + "'";
        }
    }
    else if (typeof data === "string") {
        detail = "String data of length " + data.length;
        if (includeContent) {
            detail += ". Content: '" + data + "'";
        }
    }
    return detail;
}
/** @private */
export function formatArrayBuffer(data) {
    var view = new Uint8Array(data);
    // Uint8Array.map only supports returning another Uint8Array?
    var str = "";
    view.forEach(function (num) {
        var pad = num < 16 ? "0" : "";
        str += "0x" + pad + num.toString(16) + " ";
    });
    // Trim of trailing space.
    return str.substr(0, str.length - 1);
}
// Also in signalr-protocol-msgpack/Utils.ts
/** @private */
export function isArrayBuffer(val) {
    return val && typeof ArrayBuffer !== "undefined" &&
        (val instanceof ArrayBuffer ||
            // Sometimes we get an ArrayBuffer that doesn't satisfy instanceof
            (val.constructor && val.constructor.name === "ArrayBuffer"));
}
/** @private */
export function sendMessage(logger, transportName, httpClient, url, accessTokenFactory, content, logMessageContent, withCredentials, defaultHeaders) {
    return __awaiter(this, void 0, void 0, function () {
        var _a, headers, token, _b, name, value, responseType, response;
        return __generator(this, function (_c) {
            switch (_c.label) {
                case 0:
                    headers = {};
                    if (!accessTokenFactory) return [3 /*break*/, 2];
                    return [4 /*yield*/, accessTokenFactory()];
                case 1:
                    token = _c.sent();
                    if (token) {
                        headers = (_a = {},
                            _a["Authorization"] = "Bearer " + token,
                            _a);
                    }
                    _c.label = 2;
                case 2:
                    _b = getUserAgentHeader(), name = _b[0], value = _b[1];
                    headers[name] = value;
                    logger.log(LogLevel.Trace, "(" + transportName + " transport) sending data. " + getDataDetail(content, logMessageContent) + ".");
                    responseType = isArrayBuffer(content) ? "arraybuffer" : "text";
                    return [4 /*yield*/, httpClient.post(url, {
                            content: content,
                            headers: __assign({}, headers, defaultHeaders),
                            responseType: responseType,
                            withCredentials: withCredentials,
                        })];
                case 3:
                    response = _c.sent();
                    logger.log(LogLevel.Trace, "(" + transportName + " transport) request complete. Response status: " + response.statusCode + ".");
                    return [2 /*return*/];
            }
        });
    });
}
/** @private */
export function createLogger(logger) {
    if (logger === undefined) {
        return new ConsoleLogger(LogLevel.Information);
    }
    if (logger === null) {
        return NullLogger.instance;
    }
    if (logger.log) {
        return logger;
    }
    return new ConsoleLogger(logger);
}
/** @private */
var SubjectSubscription = /** @class */ (function () {
    function SubjectSubscription(subject, observer) {
        this.subject = subject;
        this.observer = observer;
    }
    SubjectSubscription.prototype.dispose = function () {
        var index = this.subject.observers.indexOf(this.observer);
        if (index > -1) {
            this.subject.observers.splice(index, 1);
        }
        if (this.subject.observers.length === 0 && this.subject.cancelCallback) {
            this.subject.cancelCallback().catch(function (_) { });
        }
    };
    return SubjectSubscription;
}());
export { SubjectSubscription };
/** @private */
var ConsoleLogger = /** @class */ (function () {
    function ConsoleLogger(minimumLogLevel) {
        this.minimumLogLevel = minimumLogLevel;
        this.outputConsole = console;
    }
    ConsoleLogger.prototype.log = function (logLevel, message) {
        if (logLevel >= this.minimumLogLevel) {
            switch (logLevel) {
                case LogLevel.Critical:
                case LogLevel.Error:
                    this.outputConsole.error("[" + new Date().toISOString() + "] " + LogLevel[logLevel] + ": " + message);
                    break;
                case LogLevel.Warning:
                    this.outputConsole.warn("[" + new Date().toISOString() + "] " + LogLevel[logLevel] + ": " + message);
                    break;
                case LogLevel.Information:
                    this.outputConsole.info("[" + new Date().toISOString() + "] " + LogLevel[logLevel] + ": " + message);
                    break;
                default:
                    // console.debug only goes to attached debuggers in Node, so we use console.log for Trace and Debug
                    this.outputConsole.log("[" + new Date().toISOString() + "] " + LogLevel[logLevel] + ": " + message);
                    break;
            }
        }
    };
    return ConsoleLogger;
}());
export { ConsoleLogger };
/** @private */
export function getUserAgentHeader() {
    var userAgentHeaderName = "X-SignalR-User-Agent";
    if (Platform.isNode) {
        userAgentHeaderName = "User-Agent";
    }
    return [userAgentHeaderName, constructUserAgent(VERSION, getOsName(), getRuntime(), getRuntimeVersion())];
}
/** @private */
export function constructUserAgent(version, os, runtime, runtimeVersion) {
    // Microsoft SignalR/[Version] ([Detailed Version]; [Operating System]; [Runtime]; [Runtime Version])
    var userAgent = "Microsoft SignalR/";
    var majorAndMinor = version.split(".");
    userAgent += majorAndMinor[0] + "." + majorAndMinor[1];
    userAgent += " (" + version + "; ";
    if (os && os !== "") {
        userAgent += os + "; ";
    }
    else {
        userAgent += "Unknown OS; ";
    }
    userAgent += "" + runtime;
    if (runtimeVersion) {
        userAgent += "; " + runtimeVersion;
    }
    else {
        userAgent += "; Unknown Runtime Version";
    }
    userAgent += ")";
    return userAgent;
}
function getOsName() {
    if (Platform.isNode) {
        switch (process.platform) {
            case "win32":
                return "Windows NT";
            case "darwin":
                return "macOS";
            case "linux":
                return "Linux";
            default:
                return process.platform;
        }
    }
    else {
        return "";
    }
}
function getRuntimeVersion() {
    if (Platform.isNode) {
        return process.versions.node;
    }
    return undefined;
}
function getRuntime() {
    if (Platform.isNode) {
        return "NodeJS";
    }
    else {
        return "Browser";
    }
}
//# sourceMappingURL=Utils.js.map