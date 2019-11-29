#!/usr/bin/env python3
"""
subtext.admin - Subtext admin API.
"""
from typing import Optional
import requests, base64, requests, hashlib
from uuid import UUID
from datetime import datetime

from .common import _assert_compatibility, VersionError, APIError, PagedList

class AdminAPI:
	"""
	Subtext admin API class.
	"""
	def __init__(self, url: str, version: str, **config):
		self.url = url
		self.version = version
		self.config = config
	
	def login_challenge(self, admin_id: UUID):
		"""
		Get a login challenge for the admin.
		"""
		resp = requests.get(self.url + "/Subtext/admin/login/challenge", params={
			'adminId': admin_id
		})
		if resp.status_code != 200:
			if resp.headers['Content-Type'].startswith('application/json'):
				raise APIError(resp.json()['error'], resp.status_code)
			else:
				raise APIError(resp.text, resp.status_code)
		return resp.json()
	
	def login_response(self, admin_id: UUID, response: bytes):
		"""
		Respond to the login challenge for the admin.
		"""
		resp = requests.post(self.url + "/Subtext/admin/login/response", params={
			'adminId': admin_id,
			'response': base64.b64encode(response)
		})
		if resp.status_code != 200:
			if resp.headers['Content-Type'].startswith('application/json'):
				raise APIError(resp.json()['error'], resp.status_code)
			else:
				raise APIError(resp.text, resp.status_code)
		return resp.json()
	
	def login(self, admin_id: UUID, secret: bytes):
		"""
		Log in as the admin using the given secret.
		"""
		result = self.login_challenge(admin_id)
		
		challenge = base64.b64decode(result['challenge'])
		response = hashlib.pbkdf2_hmac('sha1', secret, challenge, self.config['pbkdf2_iterations'], self.config['secret_size'])
		
		result = self.login_response(admin_id, response)
		return UUID(result['sessionId'])
	
	def renew(self, session_id: UUID):
		"""
		Renew the admin session.
		"""
		resp = requests.post(self.url + "/Subtext/admin/renew", params={
			'sessionId': session_id
		})
		if resp.status_code != 200:
			if resp.headers['Content-Type'].startswith('application/json'):
				raise APIError(resp.json()['error'], resp.status_code)
			else:
				raise APIError(resp.text, resp.status_code)
		return resp.json()
	
	def logout(self, session_id: UUID):
		"""
		Log out of the admin session.
		"""
		resp = requests.post(self.url + "/Subtext/admin/logout", params={
			'sessionId': session_id
		})
		if resp.status_code != 200:
			print(resp.headers)
			if resp.headers['Content-Type'].startswith('application/json'):
				raise APIError(resp.json()['error'], resp.status_code)
			else:
				raise APIError(resp.text, resp.status_code)
		return resp.json()
	
	def audit_log(self, session_id: UUID, start: int = 0, count: int = 0, action: Optional[str] = None, admin_id: Optional[UUID] = None, start_time: Optional[datetime] = None, end_time: Optional[datetime] = None):
		"""
		Retrieve audit log entries.
		"""
		resp = requests.get(self.url + "/Subtext/admin/auditlog", params={
			'sessionId': session_id,
			'start': start,
			'count': count,
			'action': action,
			'adminId': admin_id,
			'startTime': start_time,
			'endTime': end_time
		})
		if resp.status_code != 200:
			if resp.headers['Content-Type'].startswith('application/json'):
				raise APIError(resp.json()['error'], resp.status_code)
			else:
				raise APIError(resp.text, resp.status_code)
		return resp.json()
