#!/usr/bin/env python3
"""
subtext.common - Common functions, classes and exceptions
"""
from typing import Callable, List, Any, Type
import collections.abc
from datetime import datetime
import base64
import iso8601
from uuid import UUID

class VersionError(Exception):
	"""
	Server version is incompatible with library or method.
	"""

class APIError(Exception):
	"""
	Generic API error.
	"""
	def __init__(self, message: str, status_code: int):
		self.message = message
		self.status_code = status_code

def _assert_compatibility(serv_ver: str, req_ver: str, *, is_module: bool = False):
	serv_major, serv_minor, serv_patch = tuple(map(int, serv_ver.split('.')))
	req_major, req_minor, req_patch = tuple(map(int, req_ver.split('.')))
	
	if serv_major != req_major:
		raise VersionError("server version {} incompatible with version {}".format(serv_ver, req_ver))
	if serv_major < 1:
		if serv_minor != req_minor or serv_patch != req_patch:
			raise VersionError("server version {} incompatible with version {}".format(serv_ver, req_ver))
	elif not is_module:
		if serv_minor < req_minor:
			raise VersionError("server version {} incompatible with version {}".format(serv_ver, req_ver))
		if serv_minor == req_minor and serv_patch < req_patch:
			raise VersionError("server version {} incompatible with version {}".format(serv_ver, req_ver))

class PagedList(collections.abc.Iterable):
	"""
	Iterable that retrieves chunks of values from a function
	"""
	def __init__(self, callback: Callable[[int], List[Any]]):
		self.__callback = callback
		self.__list = []
	def __getitem__(self, index: int) -> Any:
		if not isinstance(index, int):
			raise TypeError("index must be int")
		while index >= len(self.__list):
			if not self.__fetch():
				raise IndexError("index out of range")
		return self.__list[index]
	def __iter__(self) -> Any:
		for item in self.__list:
			yield item
		start = len(self.__list)
		while True:
			page = self.__callback(start)
			start += len(page)
			if len(page) <= 0:
				break
			for item in page:
				self.__list.append(item)
				yield item
	def __fetch(self) -> bool:
		page = self.__callback(len(self.__list))
		if len(page) <= 0:
			return False
		self.__list.extend(page)
		return True

class Translator:
	@staticmethod
	def to_subtext(value: Any) -> Any:
		if isinstance(value, (bytes, bytearray)):
			return base64.b64encode(value)
		elif isinstance(value, datetime):
			return datetime().isoformat()
		return value
	
	@staticmethod
	def from_subtext(value: str, type: Type) -> Any:
		if type in (bytes, bytearray):
			return base64.b64decode(value)
		elif type == datetime:
			return iso8601.parse_date(value)
		elif type == UUID:
			return UUID(value)
		return value
