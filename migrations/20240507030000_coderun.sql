-- +goose NO TRANSACTION

-- +goose Up

CREATE TABLE IF NOT EXISTS "coderuns"
(
    id           uuid primary key,
    code         text,
    scheduled_at timestamp without time zone
);

-- +goose Down