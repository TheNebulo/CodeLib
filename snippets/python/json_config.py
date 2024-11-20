import json, inspect, os

def get_config(subconfig: str = None, rule: str = None):
	"""Loads the configuration JSON, or a specfic subconfig or rule.
	The config objects looks like so:
	{
		"subconfig1" : {
			"rule1": "value",
			"rule2": "value"
		}
 	}

	Args:
		subconfig (str, optional): The key of the subconfig section. Defaults to None.
		rule (str, optional): The key of the rule to load. Defaults to None.
	"""
	try:
		with open("config.json", "r") as f:
			config = json.load(f)
		if subconfig: subconfig = config[subconfig]
		if rule: return subconfig[rule]
		if subconfig: return subconfig
		return config
	except:
		file = os.path.splitext(os.path.basename(inspect.stack()[1].filename))[0]
		func = inspect.stack()[1].function
		src = f"{file}.{func}"
		# log(f'Failed config load from {src} trying to load subconfig: {subconfig} and rule: {rule}', 'error') Implement your own loggin solution
		return None