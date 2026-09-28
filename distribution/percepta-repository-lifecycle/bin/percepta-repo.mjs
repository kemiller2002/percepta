#!/usr/bin/env node

import { createHash } from "node:crypto";
import { chmod, mkdir, readFile, rename, rm, writeFile } from "node:fs/promises";
import { homedir } from "node:os";
import { dirname, join, resolve } from "node:path";
import { spawn } from "node:child_process";
import { fileURLToPath } from "node:url";

export const VERSION = "0.1.0";
export const OWNER = "kemiller2002";
export const REPOSITORY = "percepta";

export function runtimeId(platform = process.platform, arch = process.arch) {
  const platformName =
    platform === "linux" ? "linux" :
    platform === "darwin" ? "osx" :
    platform === "win32" ? "win" :
    null;

  const archName =
    arch === "x64" ? "x64" :
    arch === "arm64" ? "arm64" :
    null;

  if (!platformName || !archName) {
    throw new Error(`Unsupported Percepta repository lifecycle platform: ${platform}/${arch}`);
  }

  if (platformName === "win" && archName === "arm64") {
    throw new Error("Percepta repository lifecycle does not yet publish win-arm64.");
  }

  return `${platformName}-${archName}`;
}

export function assetName(rid) {
  return `percepta-repo-${rid}${rid.startsWith("win-") ? ".exe" : ""}`;
}

export function releaseTag(version = VERSION) {
  return `percepta-repo-v${version}`;
}

export function parseChecksum(checksums, asset) {
  for (const rawLine of checksums.split(/\r?\n/u)) {
    const line = rawLine.trim();
    if (!line) continue;

    const match = /^([0-9a-fA-F]{64})\s+\*?(.+)$/u.exec(line);
    if (match && match[2] === asset) {
      return match[1].toLowerCase();
    }
  }

  throw new Error(`Checksum for ${asset} was not present in checksums.txt.`);
}

export function sha256(bytes) {
  return createHash("sha256").update(bytes).digest("hex");
}

async function download(url) {
  const response = await fetch(url, { redirect: "follow" });

  if (!response.ok) {
    throw new Error(`Request to ${url} failed: ${response.status} ${response.statusText}`);
  }

  return Buffer.from(await response.arrayBuffer());
}

async function ensureBinary() {
  const rid = runtimeId();
  const asset = assetName(rid);
  const tag = releaseTag();
  const root = process.env.PERCEPTA_REPO_CACHE_DIR
    ? resolve(process.env.PERCEPTA_REPO_CACHE_DIR)
    : join(homedir(), ".cache", "percepta-repo", VERSION, rid);

  const binaryPath = join(root, asset);
  const checksumPath = join(root, "checksums.txt");
  const markerPath = join(root, ".verified");

  try {
    const marker = await readFile(markerPath, "utf8");
    if (marker.trim() === VERSION) {
      return binaryPath;
    }
  } catch {
    // Cache miss. Download and verify below.
  }

  await mkdir(root, { recursive: true });

  const base = `https://github.com/${OWNER}/${REPOSITORY}/releases/download/${tag}`;
  const [binaryBytes, checksumBytes] = await Promise.all([
    download(`${base}/${asset}`),
    download(`${base}/checksums.txt`)
  ]);

  const checksums = checksumBytes.toString("utf8");
  const expected = parseChecksum(checksums, asset);
  const actual = sha256(binaryBytes);

  if (actual !== expected) {
    throw new Error(`SHA-256 mismatch for ${asset}: expected ${expected}, got ${actual}.`);
  }

  const tempBinary = `${binaryPath}.${process.pid}.tmp`;
  const tempChecksums = `${checksumPath}.${process.pid}.tmp`;
  await writeFile(tempBinary, binaryBytes);
  await writeFile(tempChecksums, checksumBytes);

  if (!rid.startsWith("win-")) {
    await chmod(tempBinary, 0o755);
  }

  await rename(tempBinary, binaryPath);
  await rename(tempChecksums, checksumPath);
  await writeFile(markerPath, `${VERSION}\n`, "utf8");

  return binaryPath;
}

export async function main(args = process.argv.slice(2)) {
  let binary;

  try {
    binary = await ensureBinary();
  } catch (error) {
    console.error(
      `percepta-repo: failed to obtain the ${process.platform}/${process.arch} binary for ${VERSION}: ${error instanceof Error ? error.message : String(error)}`
    );
    return 1;
  }

  return await new Promise((resolveExit) => {
    const child = spawn(binary, args, {
      stdio: "inherit",
      env: process.env
    });

    child.once("error", (error) => {
      console.error(`percepta-repo: failed to start lifecycle binary: ${error.message}`);
      resolveExit(1);
    });

    child.once("exit", (code, signal) => {
      if (signal) {
        console.error(`percepta-repo: lifecycle binary terminated by ${signal}`);
        resolveExit(1);
      } else {
        resolveExit(code ?? 1);
      }
    });
  });
}

const entry = process.argv[1] ? resolve(process.argv[1]) : null;
if (entry && entry === fileURLToPath(import.meta.url)) {
  process.exitCode = await main();
}
