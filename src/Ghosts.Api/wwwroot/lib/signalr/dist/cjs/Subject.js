"use strict";
// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
Object.defineProperty(exports, "__esModule", { value: true });
var Utils_1 = require("./Utils");
/** Stream implementation to stream items to the server. */
var Subject = /** @class */ (function () {
    function Subject() {
        this.observers = [];
    }
    Subject.prototype.next = function (item) {
        for (var _i = 0, _a = this.observers; _i < _a.length; _i++) {
            var observer = _a[_i];
            observer.next(item);
        }
    };
    Subject.prototype.error = function (err) {
        for (var _i = 0, _a = this.observers; _i < _a.length; _i++) {
            var observer = _a[_i];
            if (observer.error) {
                observer.error(err);
            }
        }
    };
    Subject.prototype.complete = function () {
        for (var _i = 0, _a = this.observers; _i < _a.length; _i++) {
            var observer = _a[_i];
            if (observer.complete) {
                observer.complete();
            }
        }
    };
    Subject.prototype.subscribe = function (observer) {
        this.observers.push(observer);
        return new Utils_1.SubjectSubscription(this, observer);
    };
    return Subject;
}());
exports.Subject = Subject;
//# sourceMappingURL=Subject.js.map