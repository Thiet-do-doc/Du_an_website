pipeline {
    agent { label 'mylove-lab' }
    options {
        skipDefaultCheckout(true)
        disableConcurrentBuilds()
        timeout(time: 30, unit: 'MINUTES')
        buildDiscarder(logRotator(numToKeepStr: '15', artifactNumToKeepStr: '5'))
        timestamps()
    }
    triggers { pollSCM('H/5 * * * *') }
    parameters {
        booleanParam(name: 'DEMO_TEST_FAILURE', defaultValue: false,
            description: 'Create one intentionally failing test in the workspace for the lab; source is unchanged.')
    }
    environment {
        CI = 'true'
        DOTNET_CLI_TELEMETRY_OPTOUT = '1'
        DOTNET_NOLOGO = '1'
    }
    stages {
        stage('Checkout GitHub') {
            steps {
                deleteDir()
                checkout scm
                sh 'git rev-parse HEAD; dotnet --version; node --version; npm --version'
            }
        }
        stage('Restore') {
            steps {
                sh 'dotnet restore DatingApp.slnx'
                dir('frontend') { sh 'npm ci' }
            }
        }
        stage('Build') {
            steps {
                sh 'dotnet build DatingApp.slnx --configuration Release --no-restore'
                dir('frontend') { sh 'npm run build' }
            }
        }
        stage('Prepare test database') {
            steps {
                sh '''
                    set +x
                    test -n "$AUTH_TEST_CONNECTION"
                    export ConnectionStrings__DefaultConnection="$AUTH_TEST_CONNECTION"
                    export ASPNETCORE_ENVIRONMENT=Testing
                    export Jwt__Key=ci-only-test-signing-key-at-least-32-bytes
                    export Auth__FrontendUrl=https://frontend.test
                    dotnet tool restore
                    dotnet ef database update --project backend/DatingApp.Api --configuration Release --no-build
                    dotnet ef migrations has-pending-model-changes --project backend/DatingApp.Api --configuration Release --no-build
                '''
            }
        }
        stage('Backend tests') {
            steps {
                catchError(buildResult: 'FAILURE', stageResult: 'FAILURE') {
                    sh 'dotnet test DatingApp.slnx --configuration Release --no-build --logger "trx;LogFileName=backend.trx" --results-directory test-results/backend'
                }
            }
            post {
                always {
                    mstest testResultsFile: 'test-results/backend/*.trx', failOnError: true, keepLongStdio: true
                }
            }
        }
        stage('Frontend tests') {
            steps {
                dir('frontend') {
                    script {
                        if (params.DEMO_TEST_FAILURE) {
                            writeFile file: 'tests/jenkins-demo.test.mjs', text: "import { test } from 'node:test';\nimport assert from 'node:assert/strict';\ntest('LAB intentional failure', () => assert.equal(1 + 1, 3));\n"
                        }
                    }
                    catchError(buildResult: 'FAILURE', stageResult: 'FAILURE') {
                        sh 'mkdir -p test-results && node --test --test-reporter=spec --test-reporter=junit --test-reporter-destination=stdout --test-reporter-destination=test-results/frontend.xml tests/*.test.mjs'
                    }
                }
            }
            post {
                always { junit testResults: 'frontend/test-results/frontend.xml', allowEmptyResults: false }
            }
        }
        stage('Package') {
            when { expression { currentBuild.currentResult == 'SUCCESS' } }
            steps {
                sh 'dotnet publish backend/DatingApp.Api --configuration Release --no-build --output artifacts/api'
                sh 'tar -czf artifacts/frontend.tar.gz -C frontend/dist .'
                sh 'tar -czf artifacts/backend.tar.gz -C artifacts/api .'
                archiveArtifacts artifacts: 'artifacts/*.tar.gz', fingerprint: true
            }
        }
    }
    post {
        always {
            archiveArtifacts artifacts: 'test-results/**/*.trx,frontend/test-results/*.xml', allowEmptyArchive: true
        }
    }
}
