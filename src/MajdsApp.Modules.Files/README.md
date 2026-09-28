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

## Storage providers (FR-FILE-001)

Everything that keeps a file (uploads, exports, profile pictures) talks to `IFileStorage` (in `MajdsApp.SharedKernel.Files`): save a stream, open a stream, delete, list. `Files:Storage:Provider` picks the store; `Disk` (the default, under `App_Data/files`) is built in. To add another (Azure Blob, S3), write a class that implements `IFileStorage` and an `IFileStorageProvider` with a name, register the provider in a module, and set the configuration to that name. An unknown name stops the application from starting and lists the ones that exist.

## What an upload is checked against (FR-FILE-002)

The bytes decide, not the name or the client's content type. A Windows, Linux or macOS program, a Java class or a script with a shebang is refused whatever the file is called. A file named as a PNG, JPEG, GIF, WebP, PDF, ZIP, DOCX, XLSX or PPTX must start like one. The content type stored (and served on download) comes from what was found; text formats (`.txt .csv .json .md .xml`) get their own type and everything else `application/octet-stream`. The size cap (`Files.MaxUploadMb`) and the blocked-extension list still apply. A refused file leaves nothing behind in the store.
