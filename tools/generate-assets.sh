#!/bin/bash
set -e

# Deps
xbps-install -Sy ImageMagick > /dev/null

MODIFIED_FILES="$(git diff --name-only -r HEAD^1 HEAD)"
PUSH_COMMIT=0
for file in $MODIFIED_FILES; do
  if [ "$file" == "res/debug/icon.svg" ] || [ "$file" == "res/release/icon.svg" ]; then
    echo "Generating assets for $file"
    PUSH_COMMIT=1

    # Convert to PNG
    convert "$file" -size 1024x1024 "${file%.*}.png"

    # Convert to ICO
    # https://stackoverflow.com/a/15104985
    convert "$file" -bordercolor white -border 0 \
      \( -clone 0 -resize 16x16 \) \
      \( -clone 0 -resize 32x32 \) \
      \( -clone 0 -resize 48x48 \) \
      \( -clone 0 -resize 64x64 \) \
      -delete 0 -alpha off -colors 256 "${file%.*}.ico"
  fi
done

if [ "$PUSH_COMMIT" == "1" ]; then
  git config --global user.email "github-actions[bot]@users.noreply.github.com"
  git config --global user.name "github-actions[bot]"
  git add .github/data/commit_marks > /dev/null
  git commit -m "[ci-skip] Regenerate icon files" > /dev/null
  git push > /dev/null
fi