param(
	[string]$file
)

NET USE \\PER7204 /USER:ltc\taskscheduler task99
if(!($?)) { exit $lastExitCode }
NET USE \\PER7201.ltc /USER:ltc\taskscheduler task99
if(!($?)) { exit $lastExitCode }

echo $file
if(Test-Path $file)
{	
	$from = Split-Path -Path $file
	cd $from
	$to = $from -replace "per7204", "per7201.ltc"
	$filename = [IO.Path]::GetFileNameWithoutExtension($file)
	$destination = $to + "\" + $filename
	echo $destination
	if(Test-Path $destination)
	{
		echo "destination exists"
	}
	else
	{
		echo "testing..."
		$test_result = 7z t $file
		if($test_result -contains "Everything is Ok")
		{
			echo "zip test success"
			echo "extracting..."
			$output_param = "-o" + $destination
			echo $output_param
			$extract_result = 7z x $file $output_param -aos -mmt
			if($extract_result -contains "Everything is Ok")
			{
				echo "zip extract success"
			}
			else
			{
				echo "zip extract fail"
				echo $extract_result
				exit 1
			}
		}
		else
		{
			echo "zip test fail"
			echo $test_result
			exit 1
		}
	}
}
else
{
	echo "file not exists"
}
