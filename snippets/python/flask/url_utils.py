from flask import request
from urllib.parse import urlparse, urljoin

def is_safe_url(target: str):
	"""Returns whether a target URL is a safe URL to redirect to automaticallly. Avoid malicious redirects, but must be called during a request only.

	Args:
		target (str): Target URL for redirect.

	Returns:
		bool: Whether the URL is safe to redirect to.
	"""
	try:
		ref_url = urlparse(request.host_url)
		test_url = urlparse(urljoin(request.host_url, target))
		return test_url.scheme in ('http', 'https') and ref_url.netloc == test_url.netloc
	except:
		return False # If something goes wrong for some reason, play it safe