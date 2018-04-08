param(
	[string]$file,
	[string]$dest
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
	$filename = [IO.Path]::GetFileNameWithoutExtension($file)
	$destination_forTest = $dest + "\" + $filename
	$destination = $dest
	echo $destination
	if(Test-Path $destination_forTest)
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
