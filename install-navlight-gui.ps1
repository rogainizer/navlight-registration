[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"

Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing

[System.Windows.Forms.Application]::EnableVisualStyles()

$form = New-Object System.Windows.Forms.Form
$form.Text = "Navlight Installer"
$form.StartPosition = "CenterScreen"
$form.Size = New-Object System.Drawing.Size(720, 540)
$form.MinimumSize = New-Object System.Drawing.Size(720, 540)
$form.FormBorderStyle = [System.Windows.Forms.FormBorderStyle]::FixedDialog
$form.MaximizeBox = $false

$y = 16

$lblMode = New-Object System.Windows.Forms.Label
$lblMode.Text = "Install mode"
$lblMode.Location = New-Object System.Drawing.Point(16, $y)
$lblMode.AutoSize = $true
$form.Controls.Add($lblMode)

$cmbMode = New-Object System.Windows.Forms.ComboBox
$cmbMode.DropDownStyle = [System.Windows.Forms.ComboBoxStyle]::DropDownList
$cmbMode.Location = New-Object System.Drawing.Point(180, ($y - 3))
$cmbMode.Size = New-Object System.Drawing.Size(220, 24)
[void]$cmbMode.Items.AddRange(@("ClientOnly", "HostAndClient", "SingleUser"))
$cmbMode.SelectedIndex = 0
$form.Controls.Add($cmbMode)

$y += 38

$lblInstallRoot = New-Object System.Windows.Forms.Label
$lblInstallRoot.Text = "Install folder"
$lblInstallRoot.Location = New-Object System.Drawing.Point(16, $y)
$lblInstallRoot.AutoSize = $true
$form.Controls.Add($lblInstallRoot)

$txtInstallRoot = New-Object System.Windows.Forms.TextBox
$txtInstallRoot.Location = New-Object System.Drawing.Point(180, ($y - 3))
$txtInstallRoot.Size = New-Object System.Drawing.Size(440, 24)
$txtInstallRoot.Text = Join-Path ([Environment]::GetFolderPath("MyDocuments")) "NavlightRegistration"
$form.Controls.Add($txtInstallRoot)

$btnBrowseInstall = New-Object System.Windows.Forms.Button
$btnBrowseInstall.Text = "Browse..."
$btnBrowseInstall.Location = New-Object System.Drawing.Point(628, ($y - 4))
$btnBrowseInstall.Size = New-Object System.Drawing.Size(72, 26)
$form.Controls.Add($btnBrowseInstall)

$y += 38

$lblDbHost = New-Object System.Windows.Forms.Label
$lblDbHost.Text = "Database host"
$lblDbHost.Location = New-Object System.Drawing.Point(16, $y)
$lblDbHost.AutoSize = $true
$form.Controls.Add($lblDbHost)

$txtDbHost = New-Object System.Windows.Forms.TextBox
$txtDbHost.Location = New-Object System.Drawing.Point(180, ($y - 3))
$txtDbHost.Size = New-Object System.Drawing.Size(220, 24)
$txtDbHost.Text = "navlighthost"
$form.Controls.Add($txtDbHost)

$y += 38

$lblDbPort = New-Object System.Windows.Forms.Label
$lblDbPort.Text = "Database port"
$lblDbPort.Location = New-Object System.Drawing.Point(16, $y)
$lblDbPort.AutoSize = $true
$form.Controls.Add($lblDbPort)

$numDbPort = New-Object System.Windows.Forms.NumericUpDown
$numDbPort.Location = New-Object System.Drawing.Point(180, ($y - 3))
$numDbPort.Size = New-Object System.Drawing.Size(120, 24)
$numDbPort.Minimum = 1
$numDbPort.Maximum = 65535
$numDbPort.Value = 3306
$form.Controls.Add($numDbPort)

$y += 38

$lblDbName = New-Object System.Windows.Forms.Label
$lblDbName.Text = "Database name"
$lblDbName.Location = New-Object System.Drawing.Point(16, $y)
$lblDbName.AutoSize = $true
$form.Controls.Add($lblDbName)

$txtDbName = New-Object System.Windows.Forms.TextBox
$txtDbName.Location = New-Object System.Drawing.Point(180, ($y - 3))
$txtDbName.Size = New-Object System.Drawing.Size(220, 24)
$txtDbName.Text = "navlight-registration"
$form.Controls.Add($txtDbName)

$y += 38

$lblDbUser = New-Object System.Windows.Forms.Label
$lblDbUser.Text = "Database user"
$lblDbUser.Location = New-Object System.Drawing.Point(16, $y)
$lblDbUser.AutoSize = $true
$form.Controls.Add($lblDbUser)

$txtDbUser = New-Object System.Windows.Forms.TextBox
$txtDbUser.Location = New-Object System.Drawing.Point(180, ($y - 3))
$txtDbUser.Size = New-Object System.Drawing.Size(220, 24)
$txtDbUser.Text = "root"
$form.Controls.Add($txtDbUser)

$y += 38

$lblDbPassword = New-Object System.Windows.Forms.Label
$lblDbPassword.Text = "Database password"
$lblDbPassword.Location = New-Object System.Drawing.Point(16, $y)
$lblDbPassword.AutoSize = $true
$form.Controls.Add($lblDbPassword)

$txtDbPassword = New-Object System.Windows.Forms.TextBox
$txtDbPassword.Location = New-Object System.Drawing.Point(180, ($y - 3))
$txtDbPassword.Size = New-Object System.Drawing.Size(220, 24)
$txtDbPassword.Text = "root"
$txtDbPassword.UseSystemPasswordChar = $true
$form.Controls.Add($txtDbPassword)

$y += 44

$grpHost = New-Object System.Windows.Forms.GroupBox
$grpHost.Text = "Host install confirmation"
$grpHost.Location = New-Object System.Drawing.Point(16, $y)
$grpHost.Size = New-Object System.Drawing.Size(684, 62)
$form.Controls.Add($grpHost)

$chkDhcp = New-Object System.Windows.Forms.CheckBox
$chkDhcp.Text = "I have already created the router DHCP reservation for this host"
$chkDhcp.Location = New-Object System.Drawing.Point(12, 26)
$chkDhcp.AutoSize = $true
$grpHost.Controls.Add($chkDhcp)

$y += 74

$grpMySql = New-Object System.Windows.Forms.GroupBox
$grpMySql.Text = "MySQL package"
$grpMySql.Location = New-Object System.Drawing.Point(16, $y)
$grpMySql.Size = New-Object System.Drawing.Size(684, 98)
$form.Controls.Add($grpMySql)

$chkDownloadMySql = New-Object System.Windows.Forms.CheckBox
$chkDownloadMySql.Text = "Download MySQL automatically"
$chkDownloadMySql.Location = New-Object System.Drawing.Point(12, 26)
$chkDownloadMySql.AutoSize = $true
$chkDownloadMySql.Checked = $true
$grpMySql.Controls.Add($chkDownloadMySql)

$lblMySqlZip = New-Object System.Windows.Forms.Label
$lblMySqlZip.Text = "MySQL ZIP path"
$lblMySqlZip.Location = New-Object System.Drawing.Point(12, 60)
$lblMySqlZip.AutoSize = $true
$grpMySql.Controls.Add($lblMySqlZip)

$txtMySqlZip = New-Object System.Windows.Forms.TextBox
$txtMySqlZip.Location = New-Object System.Drawing.Point(120, 56)
$txtMySqlZip.Size = New-Object System.Drawing.Size(472, 24)
$grpMySql.Controls.Add($txtMySqlZip)

$btnBrowseMySqlZip = New-Object System.Windows.Forms.Button
$btnBrowseMySqlZip.Text = "Browse..."
$btnBrowseMySqlZip.Location = New-Object System.Drawing.Point(600, 55)
$btnBrowseMySqlZip.Size = New-Object System.Drawing.Size(72, 26)
$grpMySql.Controls.Add($btnBrowseMySqlZip)

$lblStatus = New-Object System.Windows.Forms.Label
$lblStatus.Text = ""
$lblStatus.Location = New-Object System.Drawing.Point(16, 456)
$lblStatus.Size = New-Object System.Drawing.Size(684, 20)
$form.Controls.Add($lblStatus)

$btnInstall = New-Object System.Windows.Forms.Button
$btnInstall.Text = "Install"
$btnInstall.Location = New-Object System.Drawing.Point(538, 480)
$btnInstall.Size = New-Object System.Drawing.Size(78, 28)
$form.Controls.Add($btnInstall)

$btnCancel = New-Object System.Windows.Forms.Button
$btnCancel.Text = "Cancel"
$btnCancel.Location = New-Object System.Drawing.Point(622, 480)
$btnCancel.Size = New-Object System.Drawing.Size(78, 28)
$form.Controls.Add($btnCancel)

$folderDialog = New-Object System.Windows.Forms.FolderBrowserDialog
$zipDialog = New-Object System.Windows.Forms.OpenFileDialog
$zipDialog.Filter = "ZIP files (*.zip)|*.zip|All files (*.*)|*.*"
$zipDialog.Multiselect = $false

function Update-UiForMode {
    $mode = [string]$cmbMode.SelectedItem

    $isClientOnly = $mode -eq "ClientOnly"
    $isHostAndClient = $mode -eq "HostAndClient"
    $isSingleUser = $mode -eq "SingleUser"

    $grpHost.Visible = $isHostAndClient
    $grpMySql.Visible = -not $isClientOnly

    if ($isSingleUser) {
        $txtDbHost.Text = "localhost"
        $txtDbHost.Enabled = $false
    }
    else {
        if (-not $txtDbHost.Enabled -and [string]::IsNullOrWhiteSpace($txtDbHost.Text)) {
            $txtDbHost.Text = "navlighthost"
        }

        $txtDbHost.Enabled = $true
    }

    $isManualZip = -not $chkDownloadMySql.Checked
    $lblMySqlZip.Enabled = $isManualZip
    $txtMySqlZip.Enabled = $isManualZip
    $btnBrowseMySqlZip.Enabled = $isManualZip
}

$cmbMode.add_SelectedIndexChanged({
    Update-UiForMode
})

$chkDownloadMySql.add_CheckedChanged({
    Update-UiForMode
})

$btnBrowseInstall.add_Click({
    $folderDialog.SelectedPath = $txtInstallRoot.Text
    if ($folderDialog.ShowDialog($form) -eq [System.Windows.Forms.DialogResult]::OK) {
        $txtInstallRoot.Text = $folderDialog.SelectedPath
    }
})

$btnBrowseMySqlZip.add_Click({
    if ($zipDialog.ShowDialog($form) -eq [System.Windows.Forms.DialogResult]::OK) {
        $txtMySqlZip.Text = $zipDialog.FileName
    }
})

$btnCancel.add_Click({
    $form.Close()
})

$btnInstall.add_Click({
    $mode = [string]$cmbMode.SelectedItem
    $installRoot = $txtInstallRoot.Text.Trim()
    $databaseHost = $txtDbHost.Text.Trim()
    $databaseName = $txtDbName.Text.Trim()
    $databaseUser = $txtDbUser.Text.Trim()
    $databasePasswordText = $txtDbPassword.Text
    $mysqlZipPath = $txtMySqlZip.Text.Trim()
    $shouldDownloadMySql = [bool]$chkDownloadMySql.Checked

    if ([string]::IsNullOrWhiteSpace($installRoot)) {
        [System.Windows.Forms.MessageBox]::Show($form, "Please choose an install folder.", "Validation", [System.Windows.Forms.MessageBoxButtons]::OK, [System.Windows.Forms.MessageBoxIcon]::Warning) | Out-Null
        return
    }

    if ($mode -ne "SingleUser" -and [string]::IsNullOrWhiteSpace($databaseHost)) {
        [System.Windows.Forms.MessageBox]::Show($form, "Please enter the database host name.", "Validation", [System.Windows.Forms.MessageBoxButtons]::OK, [System.Windows.Forms.MessageBoxIcon]::Warning) | Out-Null
        return
    }

    if ([string]::IsNullOrWhiteSpace($databaseName)) {
        [System.Windows.Forms.MessageBox]::Show($form, "Please enter the database name.", "Validation", [System.Windows.Forms.MessageBoxButtons]::OK, [System.Windows.Forms.MessageBoxIcon]::Warning) | Out-Null
        return
    }

    if ([string]::IsNullOrWhiteSpace($databaseUser)) {
        [System.Windows.Forms.MessageBox]::Show($form, "Please enter the database user.", "Validation", [System.Windows.Forms.MessageBoxButtons]::OK, [System.Windows.Forms.MessageBoxIcon]::Warning) | Out-Null
        return
    }

    if ($mode -eq "HostAndClient" -and -not $chkDhcp.Checked) {
        [System.Windows.Forms.MessageBox]::Show($form, "Host installs require confirmation that the router DHCP reservation has already been created.", "Validation", [System.Windows.Forms.MessageBoxButtons]::OK, [System.Windows.Forms.MessageBoxIcon]::Warning) | Out-Null
        return
    }

    if ($mode -in @("HostAndClient", "SingleUser") -and -not $shouldDownloadMySql) {
        if ([string]::IsNullOrWhiteSpace($mysqlZipPath)) {
            [System.Windows.Forms.MessageBox]::Show($form, "Please select a MySQL ZIP file path, or enable automatic download.", "Validation", [System.Windows.Forms.MessageBoxButtons]::OK, [System.Windows.Forms.MessageBoxIcon]::Warning) | Out-Null
            return
        }

        if (-not (Test-Path -LiteralPath $mysqlZipPath)) {
            [System.Windows.Forms.MessageBox]::Show($form, "The selected MySQL ZIP file does not exist.", "Validation", [System.Windows.Forms.MessageBoxButtons]::OK, [System.Windows.Forms.MessageBoxIcon]::Warning) | Out-Null
            return
        }
    }

    $installScriptPath = Join-Path $PSScriptRoot "install-navlight.ps1"
    if (-not (Test-Path -LiteralPath $installScriptPath)) {
        [System.Windows.Forms.MessageBox]::Show($form, "install-navlight.ps1 was not found in this folder.", "Installer Error", [System.Windows.Forms.MessageBoxButtons]::OK, [System.Windows.Forms.MessageBoxIcon]::Error) | Out-Null
        return
    }

    $arguments = @{
        InstallMode                 = $mode
        InstallRoot                 = $installRoot
        DatabasePort                = [int]$numDbPort.Value
        DatabaseName                = $databaseName
        DatabaseUser                = $databaseUser
        DatabasePassword            = (ConvertTo-SecureString $databasePasswordText -AsPlainText -Force)
        NonInteractive              = $true
        DownloadMySqlAutomatically  = $shouldDownloadMySql
        DhcpReservationConfirmed    = [bool]$chkDhcp.Checked
    }

    if ($mode -ne "SingleUser") {
        $arguments.DatabaseServer = $databaseHost
    }

    if ($mode -in @("HostAndClient", "SingleUser") -and -not $shouldDownloadMySql) {
        $arguments.MySqlZipPath = $mysqlZipPath
    }

    $btnInstall.Enabled = $false
    $btnCancel.Enabled = $false
    $form.Cursor = [System.Windows.Forms.Cursors]::WaitCursor
    $lblStatus.Text = "Installing, please wait..."
    [System.Windows.Forms.Application]::DoEvents()

    try {
        & $installScriptPath @arguments
        [System.Windows.Forms.MessageBox]::Show($form, "Navlight installation completed successfully.", "Install Complete", [System.Windows.Forms.MessageBoxButtons]::OK, [System.Windows.Forms.MessageBoxIcon]::Information) | Out-Null
        $form.Close()
    }
    catch {
        $message = if ($_.Exception) { $_.Exception.Message } else { "$($_)" }
        [System.Windows.Forms.MessageBox]::Show($form, $message, "Install Failed", [System.Windows.Forms.MessageBoxButtons]::OK, [System.Windows.Forms.MessageBoxIcon]::Error) | Out-Null
        $lblStatus.Text = "Install failed."
    }
    finally {
        $form.Cursor = [System.Windows.Forms.Cursors]::Default
        $btnInstall.Enabled = $true
        $btnCancel.Enabled = $true
        if ($lblStatus.Text -eq "Installing, please wait...") {
            $lblStatus.Text = ""
        }
    }
})

Update-UiForMode
[void]$form.ShowDialog()
