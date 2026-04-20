$pwd = ConvertTo-SecureString -String "PortKill2026!" -Force -AsPlainText
Import-PfxCertificate -FilePath "D:\Projects\PowerToysCommandPalettePortKill\PortKill\JasonTiw.pfx" -CertStoreLocation "Cert:\LocalMachine\TrustedPeople" -Password $pwd
Write-Host "Certificate installed to TrustedPeople store"