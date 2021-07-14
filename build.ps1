$ErrorActionPreference = 'Stop'

pushd $PSScriptRoot

### Create test certificates.

mkdir .\certificates -Force | Out-Null

$certpwd = ConvertTo-SecureString 'password' -AsPlainText -Force

### Create root certificate.

$rootCertBaseName = '.\certificates\root'
if (-not (Test-Path -LiteralPath "$rootCertBaseName.cer")) {
    $params = @{
        Subject = 'CN=CertMemLeakRoot'
        CertStoreLocation = 'Cert:\LocalMachine\My'
        KeyExportPolicy = 'Exportable'
        KeySpec = 'Signature'
        KeyUsage = @('CertSign','CRLSign')
    }
    $rootCert = New-SelfSignedCertificate @params 
    Export-PfxCertificate -Cert $rootCert -FilePath "$rootCertBaseName.pfx" -Password $certpwd
    Export-Certificate -Cert $rootCert -FilePath "$rootCertBaseName.cer" -Type CERT
    Import-PfxCertificate "$rootCertBaseName.pfx" -Password $certpwd -Exportable -CertStoreLocation Cert:\LocalMachine\Root | ft
}

### Create intermediate certificate.

$intCertBaseName = '.\certificates\int'
if (-not (Test-Path -LiteralPath "$intCertBaseName.cer")) {
    $params = @{
        Subject = 'CN=CertMemLeakInt'
        CertStoreLocation = 'Cert:\LocalMachine\My'
        KeyExportPolicy = 'Exportable'
        KeySpec = 'Signature'
        KeyUsage = @('CertSign','CRLSign','DigitalSignature')
        TextExtension = '2.5.29.19={text}CA=true'
        Signer = $rootCert
    }
    $intCert = New-SelfSignedCertificate @params
    Export-PfxCertificate -Cert $intCert -FilePath "$intCertBaseName.pfx" -Password $certpwd
    Export-Certificate -Cert $intCert -FilePath "$intCertBaseName.cer" -Type CERT
    Import-PfxCertificate "$intCertBaseName.pfx" -Password $certpwd -Exportable -CertStoreLocation Cert:\LocalMachine\CA | ft
}

### Create client certificate.

$clientCertBaseName = '.\certificates\client'
if (-not (Test-Path -LiteralPath "$clientCertBaseName.cer")) {
    $params = @{
        Subject = 'CN=CertMemLeak'
        DnsName = 'cert.test.azstbridge.microsoft.com'
        CertStoreLocation = 'Cert:\LocalMachine\My'
        KeyExportPolicy = 'Exportable'
        KeySpec = 'Signature'
        KeyUsage = @('DigitalSignature','KeyEncipherment','DataEncipherment')
        Signer = $intCert
    }
    $clientCert = New-SelfSignedCertificate @params
    Export-PfxCertificate -Cert $clientCert -FilePath "$clientCertBaseName.pfx" -Password $certpwd
    Export-Certificate -Cert $clientCert -FilePath "$clientCertBaseName.cer" -Type CERT
}

### Remove test certificates from local host.

dir Cert:\LocalMachine -Recurse | ? { $_.Subject -like 'CN=CertMemLeak*' } | rm -Verbose

### Build the project.

dotnet publish CertMemLeak.csproj --runtime linux-x64 --interactive --output .\out\CertMemLeak

if ($LASTEXITCODE -ne 0) {
    throw "DOTNET returned $LASTEXITCODE"
}

### Build the container.

docker build -f dockerfile -t certmemleak:latest .

if ($LASTEXITCODE -ne 0) {
    throw "DOCKER returned $LASTEXITCODE"
}

popd
