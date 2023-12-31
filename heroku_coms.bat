heroku container:push web --app=glancereddit
heroku container:release web --app=glancereddit
heroku logs --tail --app=glancereddit