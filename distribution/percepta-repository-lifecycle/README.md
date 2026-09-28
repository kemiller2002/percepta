# Percepta repository lifecycle launcher

This package is the distribution shim for `percepta-repo`.

It contains no repository lifecycle policy. The launcher selects the current platform, downloads the self-contained binary from the immutable GitHub release `percepta-repo-v0.1.0`, verifies it against that release's `checksums.txt`, caches it, and forwards arguments/stdin/stdout/stderr.

Semantic UI verification remains in the separate `percepta` executable.
