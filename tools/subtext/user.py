#!/usr/bin/env python3
"""
subtext.user - Subtext user API.
"""
from typing import Optional
import requests, base64, requests, hashlib
from uuid import UUID
from datetime import datetime

from .common import _assert_compatibility, VersionError, APIError, PagedList

class UserAPI:
	"""
	Subtext user API class.
	"""
	def __init__(self, url: str, version: str, **config):
		self.url = url
		self.version = version
		self.config = config
	
	def create(self, name: str, password: str, public_key: Optional[bytes] = None):
		resp = requests.post(self.url + "/Subtext/user/create", params={
			'name': name,
			'password': password
		})
		if resp.status_code != 201:
			if resp.headers['Content-Type'].startswith('application/json'):
				raise APIError(resp.json()['error'], resp.status_code)
			else:
				raise APIError(resp.text, resp.status_code)
		return resp.json()
	
	def query_id_by_name(self, name: str):
		resp = requests.get(self.url + "/Subtext/user/queryIdByName", params={
			'name': name
		})
		if resp.status_code != 200:
			if resp.headers['Content-Type'].startswith('application/json'):
				raise APIError(resp.json()['error'], resp.status_code)
			else:
				raise APIError(resp.text, resp.status_code)
		return resp.json()
	
	def login(self, user_id: UUID, password: str):
		resp = requests.post(self.url + "/Subtext/user/login", params={
			'userId': user_id,
			'password': password
		})
		if resp.status_code != 200:
			if resp.headers['Content-Type'].startswith('application/json'):
				raise APIError(resp.json()['error'], resp.status_code)
			else:
				raise APIError(resp.text, resp.status_code)
		return resp.json()
		
	def heartbeat(self, session_id: UUID):
		resp = requests.post(self.url + "/Subtext/user/heartbeat", params={
			'sessionId': session_id
		})
		if resp.status_code != 200:
			if resp.headers['Content-Type'].startswith('application/json'):
				raise APIError(resp.json()['error'], resp.status_code)
			else:
				raise APIError(resp.text, resp.status_code)
		return resp.json()
		
	def logout(self, session_id: UUID):
		resp = requests.post(self.url + "/Subtext/user/logout", params={
			'sessionId': session_id
		})
		if resp.status_code != 200:
			if resp.headers['Content-Type'].startswith('application/json'):
				raise APIError(resp.json()['error'], resp.status_code)
			else:
				raise APIError(resp.text, resp.status_code)
		return resp.json()
		
	def get_user(self, session_id: UUID, user_id: UUID):
		resp = requests.get(self.url + "/Subtext/user/{}".format(user_id), params={
			'sessionId': session_id
		})
		if resp.status_code != 200:
			if resp.headers['Content-Type'].startswith('application/json'):
				raise APIError(resp.json()['error'], resp.status_code)
			else:
				raise APIError(resp.text, resp.status_code)
		return resp.json()
		
