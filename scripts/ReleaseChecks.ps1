function Assert-KeepClipReleaseCI {
    param(
        [Parameter(Mandatory=$true)][AllowEmptyCollection()][object[]]$Runs,
        [Parameter(Mandatory=$true)][string]$Sha
    )
    $eligible = @($Runs | Where-Object {
        $_.head_sha -ceq $Sha -and $_.head_branch -ceq 'main' -and $_.event -cin @('push','workflow_dispatch')
    } | Sort-Object -Property { [long]$_.id } -Descending)
    if ($eligible.Count -eq 0) { throw "Nessuna Windows CI trovata su main per $Sha. Esegui la CI su quel commit prima del rilascio." }
    $latest = $eligible[0]
    if ($latest.status -cne 'completed' -or $latest.conclusion -cne 'success') {
        throw "La Windows CI piu recente per $Sha non e completata con successo. Attendi o correggi la CI e riprova."
    }
    return $latest
}
