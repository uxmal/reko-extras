<?php
/**
 * Copyright(C) 2022 Stefano Moioli <smxdev4@gmail.com>
 */
function path_combine(string ...$parts){
	return implode(DIRECTORY_SEPARATOR, $parts);
}

class Gas {
	private string $binPath;
	private string $arguments = '';
	
	public function __construct(string $binPath){
		$this->binPath = $binPath;
	}

	public function setArguments(string $args){
		$this->arguments = $args;
	}

	public function assembleString(string $str){
		$pwd = sys_get_temp_dir();

		$obj = path_combine($pwd, 'a.out');
		if(file_exists($obj)) unlink($obj);

		$descs = array(
			0 => ['pipe', 'r']
		);

		$cmdline = $this->binPath;
		if(!empty($this->arguments)){
			$cmdline .= " {$this->arguments}";
		}
		$hProc = proc_open($cmdline, $descs, $pipes, $pwd);

		fwrite($pipes[0], $str . "\n");
		fclose($pipes[0]);
		proc_close($hProc);

		return $obj;
	}
	
	public function assembleFile(string $filePath){
		return $this->assembleString(file_get_contents($filePath));
	}
}

class Reko {
	/** -a x86-protected-64
	 * -o i386:x86-64:intel 
	 * --elf "G:\projects\reko-extras\RekoSifter\asm\a.out"
	 **/
	private string $arch = 'x86-protected-64';
	private string $binPath;

	public function __construct(string $binPath){
		$this->binPath = $binPath;
	}

	public function setArchitecture(string $arch){
		$this->arch = $arch;
	}

	public function run(string $filePath){
		$descs = array(
			1 => ['pipe', 'w']
		);
		$binDir = dirname($this->binPath);
		$cmdline = (''
			. escapeshellarg($this->binPath)
			. " -a {$this->arch}"
			. " -o i386:x86-64:intel"
			. " --elf " . escapeshellarg($filePath)
		);
		$hProc = proc_open($cmdline, $descs, $pipes, $binDir);

		$lastReko = '';
		$lastOther = '';

		while(!feof($pipes[1])){
			$line = rtrim(fgets($pipes[1]));
			if($line === false) continue;
			$length = strlen($line);
			if($length < 2) continue;

			$res = json_decode($line, true);
			if($res === false || $res === null) continue;

			list($id, $addr, $text, $hex) = [
				$res['Id'], $res['Address'], $res['Text'], $res['Hex']
			];
			$hex = strtolower($hex);

			switch($id){
				case 'R':
					$lastReko = [$addr, $text, $hex];
					break;
				case 'O':
					$lastOther = [$addr, $text, $hex];
					yield [$lastReko, $lastOther];
					$lastReko = '';
					$lastOther = '';
					break;
			}
		}

		proc_close($hProc);
	}
}

class RulesFile {
	private $fh;

	public function __construct(string $filePath){
		$this->fh = fopen($filePath, 'r');
		if(!is_resource($this->fh)){
			throw new Exception("Cannot open '{$filePath}' for reading");
		}
	}

	public function __destruct(){
		if(is_resource($this->fh)){
			fclose($this->fh);
		}
	}
	
	public function rules(){
		while(!feof($this->fh)){
			$line = rtrim(fgets($this->fh));
			if($line === false) continue;
			if(strlen($line) < 2) continue;
			if($line[0] !== '#') continue;
			
			$line = substr($line, 1);
			$p = explode(':', $line, 2);
			if(count($p) < 2) continue;

			list($k, $v) = $p;
			$k = trim($k);
			$v = trim($v);

			yield $k => $v;
		}
	}
}

$gas = new Gas(path_combine(__DIR__, 'asm', 'x86_64-unknown-linux-as.exe'));
$reko = new Reko(path_combine(__DIR__, 'RekoSifter', 'bin', 'x64', 'Debug', 'net5.0', 'RekoSifter.exe'));

$testsDir = $argv[1];

$tests = glob(path_combine($testsDir, '*.d'));
foreach($tests as $d){
	$info = pathinfo($d);
	
	$rules = new RulesFile($d);
	$rules = iterator_to_array($rules->rules());

	$source = path_combine($info['dirname'], $info['filename'] . ".s");
	if(isset($rules['source'])){
		$source = path_combine($info['dirname'], $rules['source']);
	}
	if(isset($rules['as'])){
		$gas->setArguments($rules['as']);
	} else {
		$gas->setArguments('');
	}

	$objPath = $gas->assembleFile($source);
	if(!file_exists($objPath)){
		fwrite(STDERR, "gas failed, skip\n");
		continue;
	}

	// start iterator
	//$it->next();

	$toObjdumpLine = function($fragment){
		list($addr, $text, $hex) = $fragment;
		$addr = sprintf("%08X", $addr);
		return "{$addr}:	{$hex}	{$text}";
	};

	$outcome = function(bool $b){
		return $b ? '✅' : '🔴';
	};

	foreach($reko->run($objPath) as $pair){
		if(false){
			if(!$it->valid()){
				break;
			}		
			/** @var string */
			$regex = '/' . $it->current() . '/';
		}
		list($out_reko, $out_other) = $pair;
		
		$reko_line = $toObjdumpLine($out_reko);
		$other_line = $toObjdumpLine($out_other);

		$cmp = $reko_line === $other_line;
		if($cmp || true){
			$reko_good = $other_good = $outcome($cmp);
		} else {
			$reko_good = $outcome(preg_match($regex, $reko_line));
			$other_good = $outcome(preg_match($regex, $other_line));
		}
		
		print("========================================\n");
		//print("T {$regex}\n");
		print("R {$reko_line} {$reko_good}\n");
		print("O {$other_line} {$other_good}\n");

		//print("R {$reko_line}\nO {$other_line}\n");
	}
	unlink($objPath);
}