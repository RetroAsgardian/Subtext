#!/usr/bin/env python3
"""
subtext - Python library for the Subtext API
"""
import requests

from . import admin
from . import user
from . import key
from . import board
from .common import _assert_compatibility, VersionError, APIError, PagedList, Translator

VERSION = "0.1.0"

class Subtext:
	"""
	Subtext main API class.
	"""
	def __init__(self, url: str, **config):
		self.url = url.rstrip("/")
		resp = requests.get(self.url)
		if resp.status_code != 200 or resp.text.strip().capitalize() != 'Subtext':
			raise ValueError("Could not detect a valid Subtext server at {}".format(self.url))
		
		self.version = self.about()['version']
		_assert_compatibility(self.version, VERSION, is_module=True)
		
		self.config = {
			'secret_size': 32,
			'pbkdf2_iterations': 10000,
		}
		self.config.update(config)
		
		self.admin = admin.AdminAPI(self.url, self.version, **self.config)
		self.user = user.UserAPI(self.url, self.version, **self.config)
		self.key = key.KeyAPI(self.url, self.version, **self.config)
		self.board = board.BoardAPI(self.url, self.version, **self.config)
	
	def about(self):
		"""
		Retrieve server information.
		"""
		resp = requests.get(self.url + "/Subtext")
		if resp.status_code // 100 != 2:
			if resp.headers['Content-Type'] == 'application/json':
				raise APIError(resp.json()['error'], resp.status_code)
			else:
				raise APIError(resp.text, resp.status_code)
		return resp.json()
