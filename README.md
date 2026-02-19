# Groovo

## Start-up instructions

This project uses Docker to run the application. To start the application, follow these steps. Make sure you have Docker installed on your machine.
1. Clone the repository and navigate to the project directory.
2. Run the following command to build and start the application:
```bash
docker compose up -d --build
```

## Deployment pipeline

The deployment pipeline is set up using GitHub Actions. Whenever changes are pushed to the main branch, the pipeline will automatically build and deploy the application to the production environment.

Following script is executed inside the machine:
```bash
cd /home/github_deploy/groovo
git fetch --all
git reset --hard origin/main

docker --version
docker compose --version

docker compose up -d --build
echo "Deployment completed successfully."
```

## Database
The application uses SQLite as the database. The database file is stored in the `./uploads/database` directory, which is mounted as a volume in the Docker container.

## Logging
The application uses Serilog for logging. Logs are stored in the `./logs` directory,

## File Uploads
The application supports file uploads. Uploaded files are stored in the `./uploads/tus` directory.

## ENV variables

```env
PORT=5000 # The port on which the application will run
ASPNETCORE_ENVIRONMENT=Production

PATH_BASE= # Base path for the application, if needed (e.g., /api)

# JWT Settings
JWT_SECRET=your-jwt-secret-key-must-be-at-least-32-characters-long

# CORS Settings, used only in development
CORS_ALLOWED_ORIGIN=http://localhost:3000

# Volume Paths, place the actual paths ON THE HOST MACHINE where you want to store the data
SQLITE_PATH=./uploads/database
UPLOADS_PATH=./uploads
LOGS_PATH=./logs
```

## Testing
To run the tests, use the following commands inside `./Tests` directory:
```bash
dotnet test --settings coverlet.runsettings --collect:"XPlat Code Coverage"
reportgenerator -reports:"TestResults/**/coverage.cobertura.xml" -targetdir:"coveragereport" -reporttypes:Html
```

### Made script for win
You can run the following script to execute the tests and generate the coverage report on Windows:
```powershell
.\Tests\run-coverage.ps1
```

## Technologies Used
- ASP.NET Core Web API
- Entity Framework Core
- Serilog for logging
- Docker for containerization
- GitHub Actions for CI/CD
- SQLite for the database
- TUS protocol for resumable file uploads
- JWT for authentication (access and refresh tokens architecture)