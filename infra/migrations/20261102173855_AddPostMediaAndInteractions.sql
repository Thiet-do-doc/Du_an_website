START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261102173855_AddPostMediaAndInteractions') THEN
    ALTER TABLE "Posts" ADD "Feeling" character varying(32);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261102173855_AddPostMediaAndInteractions') THEN
    ALTER TABLE "Posts" ADD "PhotoContent" bytea;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261102173855_AddPostMediaAndInteractions') THEN
    ALTER TABLE "Posts" ADD "PhotoType" character varying(30);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261102173855_AddPostMediaAndInteractions') THEN
    CREATE TABLE "PostComments" (
        "Id" uuid NOT NULL,
        "PostId" uuid NOT NULL,
        "UserId" uuid NOT NULL,
        "Content" character varying(1000) NOT NULL,
        "CreatedAtUtc" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_PostComments" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_PostComments_AspNetUsers_UserId" FOREIGN KEY ("UserId") REFERENCES "AspNetUsers" ("Id") ON DELETE CASCADE,
        CONSTRAINT "FK_PostComments_Posts_PostId" FOREIGN KEY ("PostId") REFERENCES "Posts" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261102173855_AddPostMediaAndInteractions') THEN
    CREATE TABLE "PostLikes" (
        "PostId" uuid NOT NULL,
        "UserId" uuid NOT NULL,
        CONSTRAINT "PK_PostLikes" PRIMARY KEY ("PostId", "UserId"),
        CONSTRAINT "FK_PostLikes_AspNetUsers_UserId" FOREIGN KEY ("UserId") REFERENCES "AspNetUsers" ("Id") ON DELETE CASCADE,
        CONSTRAINT "FK_PostLikes_Posts_PostId" FOREIGN KEY ("PostId") REFERENCES "Posts" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261102173855_AddPostMediaAndInteractions') THEN
    CREATE INDEX "IX_PostComments_PostId_CreatedAtUtc_Id" ON "PostComments" ("PostId", "CreatedAtUtc", "Id");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261102173855_AddPostMediaAndInteractions') THEN
    CREATE INDEX "IX_PostComments_UserId" ON "PostComments" ("UserId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261102173855_AddPostMediaAndInteractions') THEN
    CREATE INDEX "IX_PostLikes_UserId" ON "PostLikes" ("UserId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261102173855_AddPostMediaAndInteractions') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261102173855_AddPostMediaAndInteractions', '10.0.12');
    END IF;
END $EF$;
COMMIT;
