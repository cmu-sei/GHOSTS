#	Invoke-Userprep 
#	Version 1.0.0
#	Creator: Luke Osterritter
#	This script takes information from the guest template via vmwaretoolsd and sets that information in the guest,
#	such as IP, hostname, gateway, etc. while alos joining a domain and setting the domain user to auto login.
#



### adding some functions - adc

function Set-OfficeReArm() {
	#cscript "c:\windows\system32\slmgr.vbs -rearm"
	&"c:\Program Files (x86)\Microsoft Office\Office15\ospprearm.exe"
}


function Start-Ghosts() {
    Set-Location -Path C:\step\Ghosts
    Start-Process ghosts.client.exe -WindowStyle Hidden
}

function clear-outlookprofile() {
    if (Test-Path "$ENV:appdata\Microsoft\Outlook\") {
        Remove-Item "$ENV:appdata\Microsoft\Outlook\" -force -Recurse
    }
    if (Test-Path HKCU:\Software\Microsoft\Office\15.0\Outlook) {
        Remove-Item "HKCU:\Software\Microsoft\Office\15.0\Outlook\Profiles\*" -Recurse -Force
        Remove-ItemProperty -Path HKCU:\Software\Microsoft\Office\15.0\Outlook\Setup\ -name First-Run
    }
}


function Start-CleanUp() {
    Remove-Item C:\Step -Recurse -Force
    Remove-Item "C:\ProgramData\Microsoft\Windows\Start Menu\Programs\StartUp\step_startup.bat" -Force
    shutdown /r /f /t 2
}


$vmwaretoolsd = 'C:\Program Files\VMware\VMware Tools\vmtoolsd.exe'

$guestinfo=@{}
$guestinfo.ipaddr =(&$vmwaretoolsd --cmd 'info-get guestinfo.ipaddr' | out-string).trim()
$guestinfo.mask = (&$vmwaretoolsd --cmd 'info-get guestinfo.mask' | out-string).trim()
$guestinfo.gw = (&$vmwaretoolsd --cmd 'info-get guestinfo.gw' | out-string).trim()
$guestinfo.dns = (&$vmwaretoolsd --cmd 'info-get guestinfo.dns' | out-string).trim()
$guestinfo.hostname = (&$vmwaretoolsd --cmd 'info-get guestinfo.hostname' | out-string).trim()
$guestinfo.domain = (&$vmwaretoolsd --cmd 'info-get guestinfo.domain' | out-string).trim()
$guestinfo.fname = (&$vmwaretoolsd --cmd 'info-get guestinfo.first' | out-string).trim()
$guestinfo.lname = (&$vmwaretoolsd --cmd 'info-get guestinfo.last' | out-string).trim()
$guestinfo.domainpass = (&$vmwaretoolsd --cmd 'info-get guestinfo.password' | out-string).trim()
$guestinfo.exchangeip = (&$vmwaretoolsd --cmd 'info-get guestinfo.exchangeip' | out-string).trim()
$guestinfo.printerip = (&$vmwaretoolsd --cmd 'info-get guestinfo.printerip' | out-string).trim()
$guestinfo.epoip = (&$vmwaretoolsd --cmd 'info-get guestinfo.epoip' | out-string).trim()
$guestinfo.domainadminuser = (&$vmwaretoolsd --cmd 'info-get guestinfo.domainadminuser' | out-string).trim()
$guestinfo.domainadminpass = (&$vmwaretoolsd --cmd 'info-get guestinfo.domainadminpass' | out-string).trim()
$guestinfo.domainadminpass_ss = ConvertTo-SecureString $guestinfo.domainadminpass -AsPlainText -force
$guestinfo.mcafee = (&$vmwaretoolsd --cmd 'info-get guestinfo.mcafee' | out-string).trim()
$guestinfo.noscripts = (&$vmwaretoolsd --cmd 'info-get guestinfo.noscripts' | out-string).trim()
$guestinfo.sidchgkey = (&$vmwaretoolsd --cmd 'info-get guestinfo.sidchgkey' | out-string).trim()
$guestinfo.kmsip = (&$vmwaretoolsd --cmd 'info-get guestinfo.kmsip' | out-string).trim()
$guestinfo.proxyserver = (&$vmwaretoolsd --cmd 'info-get guestinfo.proxyserver' | out-string).trim()
$guestinfo.email = (&$vmwaretoolsd --cmd 'info-get guestinfo.email' | out-string).trim()
$guestinfo.uname = $guestinfo.fname + " " + $guestinfo.lname
$guestinfo.domainuser = $guestinfo.fname + "." + $guestinfo.lname
$guestinfo.finitial = $guestinfo.fname[0]
$guestinfo.linitial = $guestinfo.lname[0]
$guestinfo.initials = $guestinfo.finitial + $guestinfo.linitial
$orgsplit = $guestinfo.domain.IndexOf(".")
$guestinfo.org = $guestinfo.domain.Substring(0, $orgsplit)
$guestinfo.admincredential = New-Object -TypeName "System.Management.Automation.PSCredential" -argumentlist $guestinfo.domainadminuser, $guestinfo.domainadminpass_ss

$scriptstage = [Environment]::GetEnvironmentVariable("ScriptInterval", "Machine")
if ($scriptstage -eq $null){
	[Environment]::SetEnvironmentVariable("ScriptInterval", "0", "Machine")
	$scriptstage = [Environment]::GetEnvironmentVariable("ScriptInterval", "Machine")
}


If ($guestinfo.noscripts -eq 1)
	{
		write-host "The noscripts flag set. Exiting..."
		exit
	}

If ($scriptstage -eq 0){
	# Converting subnet mask to CIDR prefix because Microsoft is infuriating
	$octets = $guestinfo.mask.Split('.') | forEach {[Convert]::ToString($_, 2)}
	$maskprefix=($octets -Join '').TrimEnd('0').Length

	# Grab the proper interface by name, since we can't specify it in later cmndlets because Microsoft is infuriating
	# get rid of name thing - adc
	$interfaces = Get-NetIPInterface -AddressFamily IPv4 | ? {$_.InterfaceAlias -notlike "*loop*"}
	$interface = $interfaces[0]
	#$interface = Get-NetIPInterface -InterfaceAlias "Ethernet" -AddressFamily IPv4

	# Clear out the interface and assign it to DHCP to avoid multiple IPs on the interface, because Microsoft is infuriating.
	$interface | Remove-NetIPAddress -confirm:$False 
	$interface | Remove-NetRoute -Confirm:$False
	Set-NetIPInterface $interface -Dhcp enabled
	sleep 5


	# Finally actually set the new IP
	$interface | New-NetIPAddress -IPAddress $guestinfo.ipaddr -PrefixLength $maskprefix -DefaultGateway $guestinfo.gw

	# Set DNS server with separate cmndlet, because Microsoft is infuriating
	$interface | Set-DnsClientServerAddress -ServerAddresses $guestinfo.dns

	# Set the new hostname. Not needed since the add-computer cmdlet can add a machine with a new name
	#rename-computer -NewName $guestinfo.hostname -Force -ErrorAction silentlycontinue
	#restart-computer -confirm:$False -Force

	# Dynamically write and run a registry change to switch the auto-login user to the domain use account set from the STEP template
	$reg_file = "c:\step\PSautologon.reg"
	$domainuser = ($guestinfo.domainuser | out-string).trim()
	$domainpass = ($guestinfo.domainpass | out-string).trim()
	$org = ($guestinfo.org | out-string).trim()
	write-output "Windows Registry Editor Version 5.00" | out-file -filepath $reg_file
	write-output "" | out-file -filepath $reg_file -append -noclobber
	write-output "[HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Winlogon] " | out-file -filepath $reg_file -append -noclobber
	write-output "`"DefaultUserName`"=`"$domainuser`"" | out-file -filepath $reg_file -append -noclobber
	write-output "`"DefaultPassword`"=`"$domainpass`"" | out-file -filepath $reg_file -append -noclobber
	write-output "`"DefaultDomainName`"=`"$org`"" | out-file -filepath $reg_file -append -noclobber
	write-output "`"AutoAdminLogon`"=`"1`"" | out-file -filepath $reg_file -append -noclobber
	write-output "`"DisableCAD`"=dword:1" | out-file -filepath $reg_file -append -noclobber
	regedit.exe /S $reg_file




	# Set the script stage env variable to 1 before the AD join reboot
	[Environment]::SetEnvironmentVariable("ScriptInterval", "1", "Machine")

  
    # do some other studd - adc
    Set-OfficeReArm
    # run ansible winrm configure from uscc
    &"C:\step\scripts\ConfigureRemotingForAnsible.ps1"
    # run activate from uscc - hardcoded for now need to make better
    #&"c:\step\scripts\Activate.ps1" -KmsServer 7.7.7.7 -NtpServer time.microsoft.com
    #need hbss installer
    #do outlook

   
    


	# Join PC to the domain, but sleep for 30 seconds to allow the network adapter to settle
	start-sleep -s 30
    [Environment]::SetEnvironmentVariable("ScriptInterval", "1", "Machine")
   
    $addcomputerargs = @{
        DomainName = "$($guestinfo.domain)"
        Credential = $guestinfo.admincredential
        NewName = "$($guestinfo.hostname)"
        Confirm = $false
        Force = $true
        ErrorAction = "SilentlyContinue"
        ErrorVariable = "JoinError"
        Restart = $true
    }
	add-computer @addcomputerargs
    if ($JoinError) {
        New-Eventlog -Logname "Application" -Source "STEP"
        write-eventlog -LogName "Application" -Source "STEP" -EventID 99 -EntryType Inofmration -Message "$JoinError"
        Rename-Computer -NewName "$($guestinfo.hostname)" -Force -restart 
    }
}
	
ElseIf ($scriptstage -eq 1){ 
    if ($guestinfo.exchangeip) {
        #clear-outlookprofile
        cmdkey /generic:"MS.Outlook.15:$($guestinfo.email)" /user:"$($guestinfo.domainuser)@disa.mil" /pass:"$($guestinfo.domainpass)"
        cmdkey /generic:"MS.Outlook.15:$($guestinfo.domainuser)@disa.mil" /user:"$($guestinfo.domainuser)@disa.mil" /pass:"$($guestinfo.domainpass)"
        #cmdkey /generic:"MS.Outlook.15:$($guestinfo.domainuser)@disa.mil" /user:"DISA\$($guestinfo.domainuser)" /pass:"$($guestinfo.domainpass)"
        New-Item -Path "HKCU:\Software\Microsoft\Office\15.0\Outlook" -ItemType Key
        New-Item -Path "HKCU:\Software\Microsoft\Office\15.0\Outlook\Autodiscover" -ItemType Key
        New-ItemProperty -Path "HKCU:\Software\Microsoft\Office\15.0\Outlook\Autodiscover" -Name "ZeroConfigExchange" -value "1" -PropertyType "DWORD"
        #New-ItemProperty -Path "HKCU:\Software\Microsoft\Office\15.0\Outlook\Autodiscover" -Name "ZeroConfigExchange" -value "1" -PropertyType "DWORD"
        #Set-ItemProperty -Path "HKCU:\Software\Microsoft\Office\Common\Userinfo" -Name "UserName" -value "$($guestinfo.domainuser)@disa.mil"
        #New-ItemProperty -Path "HKCU:\Software\Microsoft\Office\Common\Userinfo" -Name "UserInitials" -value "SIM"   -PropertyType "String"
        #New-Item -Path "HKCU:\Software\Microsoft\Office\15.0\Common\Internet" -ItemType Key
        #New-ItemProperty -Path "HKCU:\Software\Microsoft\Office\15.0\Common\Internet" -Name "UseOnlineContent" -value "1"   -PropertyType "DWORD"
        #New-Item -Path "HKCU:\Software\Microsoft\Office\15.0\Common\General" -ItemType Key
        #New-ItemProperty -Path "HKCU:\Software\Microsoft\Office\15.0\Common\General" -Name "ShownFirstRunOptin" -value "1"   -PropertyType "DWORD"
        #New-Item -Path "HKCU:\Software\Policies\Microsoft\Office" -ItemType Key
        #New-Item -Path "HKCU:\Software\Policies\Microsoft\Office\15.0" -ItemType Key
        New-Item -Path "HKCU:\Software\Policies\Microsoft\Office\15.0\Outlook" -ItemType Key
        New-Item -Path "HKCU:\Software\Policies\Microsoft\Office\15.0\Outlook\Autodiscover" -ItemType Key
        New-ItemProperty -Path "HKCU:\Software\Policies\Microsoft\Office\15.0\Outlook\Autodiscover" -Name "ZeroConfigExchange" -value "1"   -PropertyType "DWORD"
    }
    [Environment]::SetEnvironmentVariable("ScriptInterval", "2", "Machine")
    #Start-CleanUp
    Start-Ghosts
	exit
}
ElseIf ($scriptstate -eq 2) {
    
    #clear-outlookprofile()
    cmdkey /generic:"MS.Outlook.15:$($guestinfo.email)" /user:"$($guestinfo.domainuser)@disa.mil" /pass:"$($guestinfo.domainpass)"
    cmdkey /generic:"MS.Outlook.15:$($guestinfo.domainuser)@disa.mil" /user:"$($guestinfo.domainuser)@disa.mil" /pass:"$($guestinfo.domainpass)"
    #cmdkey /generic:"MS.Outlook.15:$($guestinfo.domainuser)@disa.mil" /user:"DISA\$($guestinfo.domainuser)" /pass:"$($guestinfo.domainpass)"
    #& "C:\Program Files (x86)\Microsoft Office\Office15\outlook.exe"
    write-host "This script has already run on this machine. Exiting..." 
    Start-Ghosts
    exit

}
Else {
	write-host "Something happened, but I dunno what - the ScriptInterval variable is set to something weird - $scriptstage . Exiting..."
	exit
}