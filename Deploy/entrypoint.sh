#!/bin/bash

/app/Deploy/dbmigrate-docker.sh /db/content.db /db/content.db.bak /app/Deploy/dbmigrations

/app/contentapi --urls "http://0.0.0.0:5000"