#!/usr/bin/env bash
set -euo pipefail

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
module_root="$(cd "$script_dir/.." && pwd)"
repo_root="$(git -C "$module_root" rev-parse --show-toplevel)"
cache_path="OrchardCore.Crest.Icons/icons/Sources/IconifyCache"
cache_dir="$repo_root/$cache_path"

if [ ! -f "$cache_dir/collections.json" ]; then
  git -C "$repo_root" submodule update --init --depth 1 --filter=blob:none -- "$cache_path"
fi

git -C "$cache_dir" sparse-checkout set --no-cone /collections.json /json/
git -C "$cache_dir" fetch --depth 1 origin master
git -C "$cache_dir" checkout --force FETCH_HEAD
git -C "$cache_dir" sparse-checkout set --no-cone /collections.json /json/

git -C "$cache_dir" log -1 --oneline
