DO $$ BEGIN

    IF (to_regtype('manga_attribute') IS NULL) THEN
		CREATE TYPE manga_attribute AS (
			name text,
			value text
		);
	END IF;

END $$;

CREATE TABLE IF NOT EXISTS manga_cache (
    id BIGSERIAL PRIMARY KEY,

    title text not null,
    source_id text not null,
    provider text not null,
    url text not null,
    cover text not null,
    tags text[] not null default '{}',
    alt_titles text[] not null default '{}',
    description text not null default '',
    nsfw boolean not null default false,
    hash_id text not null,
    referer text,
    source_created timestamp,

    attributes manga_attribute[] not null default '{}',

    created_at timestamp not null default CURRENT_TIMESTAMP,
    updated_at timestamp not null default CURRENT_TIMESTAMP,
    deleted_at timestamp,

    CONSTRAINT uiq_manga_cache_title_hash UNIQUE(source_id, provider)
);

CREATE TABLE IF NOT EXISTS manga_chapter_cache (
    id BIGSERIAL PRIMARY KEY,

    manga_id bigint not null references manga_cache(id),
    title text not null,
    url text not null,
    source_id text not null,
    ordinal numeric not null,
    volume numeric,
    language text not null,
    pages text[] not null default '{}',
    external_url text,
    attributes manga_attribute[] not null default '{}',
    state numeric not null default 1,

    created_at timestamp not null default CURRENT_TIMESTAMP,
    updated_at timestamp not null default CURRENT_TIMESTAMP,
    deleted_at timestamp,

    CONSTRAINT uiq_manga_chapter_cache UNIQUE(manga_id, source_id, language)
);