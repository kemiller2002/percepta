import assert from "node:assert/strict";
import { assetName, parseChecksum, releaseTag, runtimeId, sha256, VERSION } from "./bin/percepta-repo.mjs";

assert.equal(VERSION, "0.1.0");
assert.equal(runtimeId("linux", "x64"), "linux-x64");
assert.equal(runtimeId("linux", "arm64"), "linux-arm64");
assert.equal(runtimeId("darwin", "x64"), "osx-x64");
assert.equal(runtimeId("darwin", "arm64"), "osx-arm64");
assert.equal(runtimeId("win32", "x64"), "win-x64");
assert.throws(() => runtimeId("win32", "arm64"), /does not yet publish/);
assert.equal(assetName("linux-x64"), "percepta-repo-linux-x64");
assert.equal(assetName("win-x64"), "percepta-repo-win-x64.exe");
assert.equal(releaseTag(), "percepta-repo-v0.1.0");

const payload = Buffer.from("percepta lifecycle fixture", "utf8");
const digest = sha256(payload);
assert.equal(parseChecksum(`${digest}  percepta-repo-linux-x64\n`, "percepta-repo-linux-x64"), digest);
assert.throws(() => parseChecksum("", "missing"), /not present/);

console.log("Percepta repository lifecycle launcher tests passed.");
