<?php
$d = file_get_contents($argv[1]);

preg_match("/enum bfd_architecture.*?\{(.*?)\};/s", $d, $m);
$d = trim($m[1]);

$lines = preg_split("/\r?\n/", $d);
$n = count($lines);

$inRegion = false;

print("[Flags]\n");
print("enum BfdMachine : uint {\n");

for($i=0; $i<$n; $i++){
	$line = trim($lines[$i]);
	if(empty($line))
		continue;

	if($line[0] === '#'){
		if(!$inRegion){
			$inRegion = true;
			
			$j = $i;
			while(!preg_match("/bfd_arch_(.*)/", $lines[--$j], $m));
			$arch = trim($m[1]);
			$arch = str_replace(",", "", $arch);
			print("\t#region {$arch}\n");
		}

		preg_match("/#define bfd_mach_(.*?)\s+(.*)/", $line, $m);
		$mach = $m[1];
		$value = $m[2];
		print("\t{$mach} = {$value},\n");
	} else {
		if($inRegion){
			$inRegion = false;
			print("\t#endregion\n");
		}
	}
}
print("}\n");