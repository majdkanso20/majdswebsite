# MajdsApp.Modules.Files

**Files (F-Files)** — SRS FR-FILE-001..006

Upload, list, download and delete files. Metadata lives in the database; bytes are stored on disk under `App_Data/files` in the API's content root, outside anything served as static content.

## API

All responses use the `ResponseDto<T>` envelope unless noted.

- `GET /api/files/download`
- `GET /api/files/list`
- `POST /api/files/delete`
- `POST /api/files/upload`

## Permissions

`Files.View`, `Files.Upload`, `Files.Delete`. A user can always download or delete a file they own; other users need the permission.

Declared here: `Files.Delete`, `Files.Upload`, `Files.View`.

## Settings defined

- `Files.MaxUploadMb` — Maximum upload size (MB)

## Data

Tables: `Files`. Migrations live in `MajdsApp.Core`.

## Notes

- Uploads are limited by the `Files.MaxUploadMb` setting (default 10) and a blocked-extension list (executables and scripts). Content is not sniffed and the client's content type is trusted.
- Deleting a file is a soft delete (FR-FILE-006): the record gets `DeletedAt`/`DeletedBy` and a global query filter hides it from every list, download, export and profile picture at once. The recurring job *Deleted file cleanup* (daily) removes the bytes and the record once the file has been deleted for `Files.DeletedRetentionDays`; if the bytes cannot be removed the record is kept and the next run tries again. There is no restore screen yet. Code that must see deleted files uses `IgnoreQueryFilters()`; the *Orphaned file cleanup* job does, so it never sweeps a soft-deleted file's bytes early.
- The whole module is behind the `Files` feature flag.
- Storage is a concrete disk class today; there is no `IFileStorage` abstraction or cloud provider yet, and deletion is permanent.

## Configuration keys

- Setting `Files.MaxUploadMb` (default `10`)
- Setting `Files.DeletedRetentionDays` (default `30`)
- Feature flag `Files`

## Tests

Not yet covered by automated tests (see the traceability document).

- *Orphaned file cleanup* (a recurring job, daily) deletes stored files that no file record points to, once they are a day old, so an upload interrupted between writing the bytes and saving the record does not leave a file behind.
