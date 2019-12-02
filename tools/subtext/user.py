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
	
	def create(self, name: str, password: str, public_key: bytes = bytes(0)):
		resp = requests.post(self.url + "/Subtext/user/create", data=public_key, params={
			'name': name,
			'password': password
		}, headers={'Content-Type': 'application/octet-stream'})
		if resp.status_code // 100 != 2:
			if resp.headers['Content-Type'].startswith('application/json'):
				raise APIError(resp.json()['error'], resp.status_code)
			else:
				raise APIError(resp.text, resp.status_code)
		return resp.json()
	
	def query_id_by_name(self, name: str):
		resp = requests.get(self.url + "/Subtext/user/queryidbyname", params={
			'name': name
		})
		if resp.status_code // 100 != 2:
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
		if resp.status_code // 100 != 2:
			if resp.headers['Content-Type'].startswith('application/json'):
				raise APIError(resp.json()['error'], resp.status_code)
			else:
				raise APIError(resp.text, resp.status_code)
		return resp.json()
		
	def heartbeat(self, session_id: UUID):
		resp = requests.post(self.url + "/Subtext/user/heartbeat", params={
			'sessionId': session_id
		})
		if resp.status_code // 100 != 2:
			if resp.headers['Content-Type'].startswith('application/json'):
				raise APIError(resp.json()['error'], resp.status_code)
			else:
				raise APIError(resp.text, resp.status_code)
		return resp.json()
		
	def logout(self, session_id: UUID):
		resp = requests.post(self.url + "/Subtext/user/logout", params={
			'sessionId': session_id
		})
		if resp.status_code // 100 != 2:
			if resp.headers['Content-Type'].startswith('application/json'):
				raise APIError(resp.json()['error'], resp.status_code)
			else:
				raise APIError(resp.text, resp.status_code)
		return resp.json()
		
	def get_user(self, session_id: UUID, user_id: UUID):
		resp = requests.get(self.url + "/Subtext/user/{}".format(user_id), params={
			'sessionId': session_id
		})
		if resp.status_code // 100 != 2:
			if resp.headers['Content-Type'].startswith('application/json'):
				raise APIError(resp.json()['error'], resp.status_code)
			else:
				raise APIError(resp.text, resp.status_code)
		return resp.json()
	
	def get_friends(self, session_id: UUID, user_id: UUID):
		resp = requests.get(self.url + "/Subtext/user/{}/friends".format(user_id), params={
			'sessionId': session_id
		})
		if resp.status_code // 100 != 2:
			if resp.headers['Content-Type'].startswith('application/json'):
				raise APIError(resp.json()['error'], resp.status_code)
			else:
				raise APIError(resp.text, resp.status_code)
		return resp.json()
	
	def get_blocked(self, session_id: UUID, user_id: UUID):
		resp = requests.get(self.url + "/Subtext/user/{}/blocked".format(user_id), params={
			'sessionId': session_id
		})
		if resp.status_code // 100 != 2:
			if resp.headers['Content-Type'].startswith('application/json'):
				raise APIError(resp.json()['error'], resp.status_code)
			else:
				raise APIError(resp.text, resp.status_code)
		return resp.json()
	
	def get_friend_requests(self, session_id: UUID, user_id: UUID):
		resp = requests.get(self.url + "/Subtext/user/{}/friendrequests".format(user_id), params={
			'sessionId': session_id
		})
		if resp.status_code // 100 != 2:
			if resp.headers['Content-Type'].startswith('application/json'):
				raise APIError(resp.json()['error'], resp.status_code)
			else:
				raise APIError(resp.text, resp.status_code)
		return resp.json()
	
	def get_user_keys(self, session_id: UUID, user_id: UUID):
		resp = requests.get(self.url + "/Subtext/user/{}/keys".format(user_id), params={
			'sessionId': session_id
		})
		if resp.status_code // 100 != 2:
			if resp.headers['Content-Type'].startswith('application/json'):
				raise APIError(resp.json()['error'], resp.status_code)
			else:
				raise APIError(resp.text, resp.status_code)
		return resp.json()
	
	def get_user_key(self, key_id: UUID):
		resp = requests.get(self.url + "/Subtext/keys/{}".format(key_id))
		if resp.status_code // 100 != 2:
			if resp.headers['Content-Type'].startswith('application/json'):
				raise APIError(resp.json()['error'], resp.status_code)
			else:
				raise APIError(resp.text, resp.status_code)
		return resp.content
	
	def post_user_key(self, session_id: UUID, user_id: UUID, public_key: bytes):
		resp = requests.post(self.url + "/Subtext/user/{}/keys".format(user_id), data=public_key, params={
			'sessionId': session_id
		}, headers={'Content-Type': 'application/octet-stream'})
		if resp.status_code // 100 != 2:
			if resp.headers['Content-Type'].startswith('application/json'):
				raise APIError(resp.json()['error'], resp.status_code)
			else:
				raise APIError(resp.text, resp.status_code)
		return resp.json()
