import jenkins.model.Jenkins
import hudson.security.HudsonPrivateSecurityRealm
import hudson.security.FullControlOnceLoggedInAuthorizationStrategy
import org.jenkinsci.plugins.workflow.job.WorkflowJob
import org.jenkinsci.plugins.workflow.cps.CpsScmFlowDefinition
import hudson.plugins.git.GitSCM
import hudson.plugins.git.UserRemoteConfig
import hudson.plugins.git.BranchSpec
import jenkins.install.InstallState

def j = Jenkins.get()
def marker = new File(j.rootDir, '.mylove-lab-configured')
if (!marker.exists()) {
    def realm = new HudsonPrivateSecurityRealm(false)
    realm.createAccount('student', new File('/run/secrets/jenkins_admin_password').text.trim())
    j.setSecurityRealm(realm)
    def strategy = new FullControlOnceLoggedInAuthorizationStrategy()
    strategy.setAllowAnonymousRead(false)
    j.setAuthorizationStrategy(strategy)
    // Single trusted student repository only. Use separate agents for production.
    j.setNumExecutors(1)
    j.setLabelString('mylove-lab')
    def job = j.createProject(WorkflowJob, 'Mylove-CI')
    def scm = new GitSCM(
        [new UserRemoteConfig('https://github.com/Thiet-do-doc/Du_an_website.git', null, null, null)],
        [new BranchSpec('*/codex/jenkins-cloud-lab')], false, [], null, null, [])
    def definition = new CpsScmFlowDefinition(scm, 'Jenkinsfile')
    definition.setLightweight(true)
    job.setDefinition(definition)
    job.setDescription('Cloud computing lab 2: GitHub checkout, build, tests and reports for Mylove.')
    job.save()
    j.setInstallState(InstallState.INITIAL_SETUP_COMPLETED)
    j.save()
    marker.text = 'configured\n'
}
