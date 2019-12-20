#!/usr/bin/env python3
"""
subtext.key - Subtext key API.
"""
import requests
from uuid import UUID

from .common import _assert_compatibility, VersionError, APIError, PagedList

class KeyAPI:
	"""
	Subtext key API class.
	"""
	def __init__(self, url: str, version: str, **config):
		self.url = url
		self.version = version
		self.config = config
	
	def get(self, key_id: UUID):
		resp = requests.get(self.url + "/Subtext/key/{}".format(key_id))
		if resp.status_code // 100 != 2:
			if resp.headers['Content-Type'].startswith('application/json'):
				raise APIError(resp.json()['error'], resp.status_code)
			else:
				raise APIError(resp.text, resp.status_code)
		return resp.content
