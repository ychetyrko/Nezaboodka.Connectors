/*---------------------------------------/
		Back References routines
--------------------------------------*/

DELIMITER //
DROP PROCEDURE IF EXISTS _before_alter_backrefs //
CREATE PROCEDURE _before_alter_backrefs()
BEGIN
DROP TEMPORARY TABLE IF EXISTS `nz_admin_db`.`backref_upd_list`;
CREATE TEMPORARY TABLE IF NOT EXISTS `nz_admin_db`.`backref_upd_list`(
	`owner_type_name` VARCHAR(128) NOT NULL
		CHECK(`owner_type_name` != ''),
	`name` VARCHAR(128) NOT NULL
		CHECK(`name` != ''),
	`back_ref_name` VARCHAR(128) DEFAULT NULL
			CHECK(`back_ref_name` != ''),

	CONSTRAINT `uc_pair`
		UNIQUE (`owner_type_name`, `name`)
);


DELIMITER //
DROP PROCEDURE IF EXISTS _update_backrefs //
CREATE PROCEDURE _update_backrefs()
BEGIN

END //
