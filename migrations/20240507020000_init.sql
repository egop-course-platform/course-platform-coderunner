-- +goose NO TRANSACTION

-- +goose Up

CREATE TABLE IF NOT EXISTS "outbox_events"
(
    id      bigserial primary key,
    type    text,
    key     text,
    date    timestamp without time zone,
    payload text,
    status  text,
    target  text
);

CREATE INDEX CONCURRENTLY IF NOT EXISTS "outbox_events_date_status" ON "outbox_events" USING btree(date, status);
CREATE INDEX CONCURRENTLY IF NOT EXISTS "outbox_events_status_date" ON "outbox_events" USING btree(status, date);


-- +goose Down