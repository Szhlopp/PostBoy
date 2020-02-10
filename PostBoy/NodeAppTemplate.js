
const createError = require("http-errors");
const express = require("express");
const path = require("path");
const fs = require("fs");
const cookieParser = require("cookie-parser");
const logger = require("morgan");
const environment = require("../../environment");
const app = express();
const appName = "###RoutefileName###";
const appRoutes = require("./###RoutefileName###Routes");

app.use(logger("dev"));
app.use(express.json());
app.use(express.urlencoded({ extended: false }));
app.use(cookieParser());
app.use(express.static(path.resolve(__dirname, "server")));

var accessLogStream = fs.createWriteStream(path.join(__dirname, `${appName}.log`), { flags: 'a' })
app.use(logger(':remote-addr [:date[iso]] [:method - :status] ":url" :res[content-length] ":user-agent"', { stream: accessLogStream }))
app.use(environment[appName].url, appRoutes);
app.get("*", (req, res) => {
    res.sendFile("../../index.html", { root: __dirname });
});

// catch 404 and forward to error handler
app.use((req, res, next) => {
    // console.log(req.url);
    next(createError(404));
});

// TODO Web Template Studio: Add your own error handler here.
if (process.env.NODE_ENV === "production") {
    // Do not send stack trace of error message when in production
    app.use((err, req, res, next) => {
        res.status(err.status || 500);
        res.send("Error occurred while handling the request.");
    });
} else {
    // Log stack trace of error message while in development
    app.use((err, req, res, next) => {
        res.status(err.status || 500);
        console.log(err);
        console.log("BLOCKED!");
        res.send(err.message + (err.status == 404 ? "\n 👻 Try checking your request method (GET/POST/etc) 👻" : ""));
    });
}

module.exports = app;
